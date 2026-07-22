package com.pocketcam.android.camera

import android.Manifest
import android.annotation.SuppressLint
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.content.pm.PackageManager
import android.net.wifi.WifiManager
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.util.Range
import android.util.Size
import androidx.camera.core.CameraSelector
import androidx.camera.core.ExperimentalSessionConfig
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.SessionConfig
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.core.resolutionselector.ResolutionSelector
import androidx.camera.core.resolutionselector.ResolutionStrategy
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.LifecycleService
import androidx.lifecycle.lifecycleScope
import com.pocketcam.android.MainActivity
import com.pocketcam.android.R
import com.pocketcam.android.pocketCamVersionName
import com.pocketcam.android.settings.SettingsStore
import com.pocketcam.android.settings.StreamSettings
import com.pocketcam.android.stream.EncodedFrame
import com.pocketcam.android.stream.FrameHub
import com.pocketcam.android.stream.ServiceStatus
import com.pocketcam.android.transport.BluetoothTransportServer
import com.pocketcam.android.transport.DiscoveryAdvertiser
import com.pocketcam.android.transport.TcpTransportServer
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.Executors
import java.util.concurrent.RejectedExecutionException
import java.util.concurrent.ThreadPoolExecutor
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong

@OptIn(ExperimentalSessionConfig::class)
class CameraStreamingService : LifecycleService() {
    private val frameHub = FrameHub()
    private val encoderThreadNumber = AtomicInteger()
    private val cameraExecutor = Executors.newSingleThreadExecutor { task ->
        priorityThread("PocketCam-Camera", task)
    }
    private val encoderExecutor = ThreadPoolExecutor(
        ENCODER_WORKERS,
        ENCODER_WORKERS,
        0,
        TimeUnit.MILLISECONDS,
        ArrayBlockingQueue(ENCODER_QUEUE_CAPACITY),
        { task -> priorityThread("PocketCam-Encoder-${encoderThreadNumber.incrementAndGet()}", task) },
        ThreadPoolExecutor.AbortPolicy(),
    )
    private val jpegEncoders = ThreadLocal.withInitial(::YuvToJpegEncoder)
    private val frameRateLimiter = FrameRateLimiter()
    private val orderedFrames = OrderedResultQueue<EncodedFrame>()
    private val frameSequence = AtomicLong()
    private val cameraGeneration = AtomicLong()
    private val cameraFrames = AtomicLong()
    private val encodedFrames = AtomicLong()
    private val droppedFrames = AtomicLong()
    private val poolLock = Any()
    private lateinit var settingsStore: SettingsStore
    private lateinit var tcpServer: TcpTransportServer
    private lateinit var bluetoothServer: BluetoothTransportServer
    private lateinit var discovery: DiscoveryAdvertiser
    private var cameraProvider: ProcessCameraProvider? = null
    private var settingsJob: Job? = null
    private var performanceJob: Job? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var wifiLock: WifiManager.WifiLock? = null
    private var multicastLock: WifiManager.MulticastLock? = null
    @Volatile
    private var bufferPoolState: BufferPoolState? = null

    override fun onCreate() {
        super.onCreate()
        settingsStore = SettingsStore(this)
        val appVersion = pocketCamVersionName()
        tcpServer = TcpTransportServer(lifecycleScope, frameHub, settingsStore, appVersion)
        bluetoothServer = BluetoothTransportServer(this, lifecycleScope, frameHub, settingsStore, appVersion)
        discovery = DiscoveryAdvertiser(this, lifecycleScope, appVersion)
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, notification())
        acquireLocks()
        ServiceStatus.update { it.copy(running = true, lastError = null) }
        tcpServer.start()
        bluetoothServer.start()
        runCatching { discovery.start() }
            .onFailure { error -> ServiceStatus.update { it.copy(lastError = "Descoberta: ${error.message}") } }
        observeCameraConfiguration()
        observePerformance()
    }

    override fun onBind(intent: Intent): IBinder? {
        super.onBind(intent)
        return null
    }

    override fun onDestroy() {
        settingsJob?.cancel()
        performanceJob?.cancel()
        cameraProvider?.unbindAll()
        cameraExecutor.shutdownNow()
        encoderExecutor.shutdownNow()
        discovery.stop()
        bluetoothServer.stop()
        tcpServer.stop()
        wifiLock?.takeIf { it.isHeld }?.release()
        multicastLock?.takeIf { it.isHeld }?.release()
        wakeLock?.takeIf { it.isHeld }?.release()
        ServiceStatus.update {
            it.copy(
                running = false,
                wifiClients = 0,
                bluetoothClients = 0,
                targetFps = 0,
                cameraFps = 0.0,
                encodedFps = 0.0,
                transmittedFps = 0.0,
                droppedFps = 0.0,
            )
        }
        super.onDestroy()
    }

    private fun observeCameraConfiguration() {
        settingsJob = lifecycleScope.launch {
            settingsStore.settings
                .map { CameraBindingSettings(it.width, it.height, it.fps, it.lens) }
                .distinctUntilChanged()
                .collect { bindCamera(settingsStore.settings.value) }
        }
    }

    private fun bindCamera(settings: StreamSettings) {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
            stopSelf()
            return
        }
        val future = ProcessCameraProvider.getInstance(this)
        future.addListener({
            try {
                val provider = future.get()
                cameraProvider = provider
                provider.unbindAll()
                synchronized(poolLock) {
                    val nextGeneration = cameraGeneration.incrementAndGet()
                    frameRateLimiter.reset()
                    bufferPoolState = null
                    orderedFrames.reset(nextGeneration, frameSequence.get())
                }
                val selector = if (settings.lens == "front") {
                    CameraSelector.DEFAULT_FRONT_CAMERA
                } else {
                    CameraSelector.DEFAULT_BACK_CAMERA
                }
                val analysisBuilder = ImageAnalysis.Builder()
                    .setResolutionSelector(
                        ResolutionSelector.Builder()
                            .setResolutionStrategy(
                                ResolutionStrategy(
                                    Size(settings.width, settings.height),
                                    ResolutionStrategy.FALLBACK_RULE_CLOSEST_HIGHER_THEN_LOWER,
                                ),
                            )
                            .build(),
                    )
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .setOutputImageFormat(ImageAnalysis.OUTPUT_IMAGE_FORMAT_NV21)
                val analysis = analysisBuilder.build()
                analysis.setAnalyzer(cameraExecutor, ::analyze)
                val baseSession = SessionConfig(analysis)
                val targetFrameRate = selectFrameRate(provider, selector, baseSession, settings.fps)
                val session = SessionConfig.Builder(listOf(analysis)).apply {
                    if (targetFrameRate != null) setFrameRateRange(targetFrameRate)
                }.build()
                provider.bindToLifecycle(this, selector, session)
                ServiceStatus.update {
                    it.copy(
                        targetFps = targetFrameRate?.upper ?: settings.fps,
                        lastError = null,
                    )
                }
            } catch (error: Exception) {
                ServiceStatus.update { it.copy(lastError = "Camera: ${error.message}") }
            }
        }, ContextCompat.getMainExecutor(this))
    }

    private fun selectFrameRate(
        provider: ProcessCameraProvider,
        selector: CameraSelector,
        session: SessionConfig,
        requestedFps: Int,
    ): Range<Int>? = runCatching {
        val cameraInfo = provider.getCameraInfo(selector)
        val selected = CameraFrameRatePolicy.select(
            cameraInfo.getSupportedFrameRateRanges(session).map { FrameRateRange(it.lower, it.upper) },
            requestedFps,
        ) ?: return@runCatching null
        Range(selected.lower, selected.upper)
    }.getOrNull()

    private fun analyze(image: ImageProxy) {
        cameraFrames.incrementAndGet()
        try {
            val settings = settingsStore.settings.value
            val now = System.nanoTime()
            val identity = synchronized(poolLock) {
                if (frameRateLimiter.shouldProcess(now, settings.fps)) {
                    cameraGeneration.get() to frameSequence.getAndIncrement()
                } else {
                    null
                }
            }
            if (identity == null) {
                droppedFrames.incrementAndGet()
                return
            }

            val (generation, sequence) = identity
            val requiredBytes = image.width * image.height * 3 / 2
            val pool = bufferPool(generation, requiredBytes)
            val buffer = pool.acquire()
            if (buffer == null) {
                droppedFrames.incrementAndGet()
                return
            }

            try {
                copyNv21(image, buffer)
            } catch (error: Exception) {
                pool.release(buffer)
                throw error
            }

            val rawFrame = RawFrame(
                generation = generation,
                sequence = sequence,
                width = image.width,
                height = image.height,
                rotation = image.imageInfo.rotationDegrees,
                quality = settings.jpegQuality,
                capturedAtMicros = System.currentTimeMillis() * 1_000,
                bytes = buffer,
                pool = pool,
            )
            try {
                encoderExecutor.execute { encode(rawFrame) }
            } catch (_: RejectedExecutionException) {
                pool.release(buffer)
                droppedFrames.incrementAndGet()
                publishCompleted(generation, sequence, null)
            }
        } catch (error: Exception) {
            ServiceStatus.update { it.copy(lastError = "Encoder: ${error.message}") }
        } finally {
            image.close()
        }
    }

    private fun encode(frame: RawFrame) {
        var encoded: EncodedFrame? = null
        try {
            val jpeg = jpegEncoders.get()!!.encode(frame.bytes, frame.width, frame.height, frame.quality)
            encoded = EncodedFrame(
                width = frame.width,
                height = frame.height,
                rotation = frame.rotation,
                jpeg = jpeg.bytes,
                jpegLength = jpeg.length,
                capturedAtMicros = frame.capturedAtMicros,
            )
        } catch (error: Exception) {
            droppedFrames.incrementAndGet()
            ServiceStatus.update { it.copy(lastError = "Encoder: ${error.message}") }
        } finally {
            frame.pool.release(frame.bytes)
            publishCompleted(frame.generation, frame.sequence, encoded)
        }
    }

    private fun publishCompleted(generation: Long, sequence: Long, frame: EncodedFrame?) {
        orderedFrames.complete(generation, sequence, frame).forEach { ready ->
            frameHub.publish(ready)
            encodedFrames.incrementAndGet()
        }
    }

    private fun bufferPool(generation: Long, requiredBytes: Int): FrameBufferPool {
        bufferPoolState?.takeIf { it.generation == generation && it.pool.bufferSize == requiredBytes }?.let {
            return it.pool
        }
        return synchronized(poolLock) {
            bufferPoolState?.takeIf { it.generation == generation && it.pool.bufferSize == requiredBytes }?.pool
                ?: FrameBufferPool(requiredBytes, ENCODER_WORKERS + ENCODER_QUEUE_CAPACITY).also { pool ->
                    bufferPoolState = BufferPoolState(generation, pool)
                }
        }
    }

    private fun observePerformance() {
        performanceJob = lifecycleScope.launch {
            var lastSample = System.nanoTime()
            while (isActive) {
                delay(1_000)
                val now = System.nanoTime()
                val elapsedSeconds = (now - lastSample) / 1_000_000_000.0
                lastSample = now
                val cameraRate = cameraFrames.getAndSet(0) / elapsedSeconds
                val encodedRate = encodedFrames.getAndSet(0) / elapsedSeconds
                val transmittedRate = ServiceStatus.takeTransmittedFrames() / elapsedSeconds
                val droppedRate = droppedFrames.getAndSet(0) / elapsedSeconds
                ServiceStatus.update {
                    it.copy(
                        cameraFps = cameraRate,
                        encodedFps = encodedRate,
                        transmittedFps = transmittedRate,
                        droppedFps = droppedRate,
                    )
                }
            }
        }
    }

    private fun createNotificationChannel() {
        getSystemService(NotificationManager::class.java).createNotificationChannel(
            NotificationChannel(CHANNEL_ID, "Transmissão da câmera", NotificationManager.IMPORTANCE_LOW),
        )
    }

    private fun notification(): Notification {
        val intent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_camera)
            .setContentTitle("PocketCam está transmitindo")
            .setContentText("USB, Wi-Fi e Bluetooth disponíveis")
            .setContentIntent(intent)
            .setOngoing(true)
            .build()
    }

    @SuppressLint("WakelockTimeout")
    @Suppress("DEPRECATION")
    private fun acquireLocks() {
        wakeLock = getSystemService(PowerManager::class.java)
            .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "PocketCam::Camera")
            .apply { acquire() }
        val wifi = applicationContext.getSystemService(WifiManager::class.java)
        val mode = if (Build.VERSION.SDK_INT >= 29) WifiManager.WIFI_MODE_FULL_LOW_LATENCY else WifiManager.WIFI_MODE_FULL_HIGH_PERF
        wifiLock = wifi.createWifiLock(mode, "PocketCam::Streaming").apply { acquire() }
        multicastLock = wifi.createMulticastLock("PocketCam::Discovery").apply {
            setReferenceCounted(false)
            acquire()
        }
    }

    companion object {
        private const val CHANNEL_ID = "streaming"
        private const val NOTIFICATION_ID = 17890
        private const val ENCODER_WORKERS = 2
        private const val ENCODER_QUEUE_CAPACITY = 2

        private fun priorityThread(name: String, task: Runnable) = Thread({
            android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_DISPLAY)
            task.run()
        }, name)
    }

    private data class CameraBindingSettings(
        val width: Int,
        val height: Int,
        val fps: Int,
        val lens: String,
    )

    private data class BufferPoolState(
        val generation: Long,
        val pool: FrameBufferPool,
    )

    private data class RawFrame(
        val generation: Long,
        val sequence: Long,
        val width: Int,
        val height: Int,
        val rotation: Int,
        val quality: Int,
        val capturedAtMicros: Long,
        val bytes: ByteArray,
        val pool: FrameBufferPool,
    )
}
