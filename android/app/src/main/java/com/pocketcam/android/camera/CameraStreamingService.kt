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
import android.util.Size
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.core.resolutionselector.ResolutionSelector
import androidx.camera.core.resolutionselector.ResolutionStrategy
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.LifecycleService
import androidx.lifecycle.lifecycleScope
import com.pocketcam.android.MainActivity
import com.pocketcam.android.R
import com.pocketcam.android.settings.SettingsStore
import com.pocketcam.android.settings.StreamSettings
import com.pocketcam.android.stream.EncodedFrame
import com.pocketcam.android.stream.FrameHub
import com.pocketcam.android.stream.ServiceStatus
import com.pocketcam.android.transport.BluetoothTransportServer
import com.pocketcam.android.transport.DiscoveryAdvertiser
import com.pocketcam.android.transport.TcpTransportServer
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.asExecutor
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicLong

class CameraStreamingService : LifecycleService() {
    private val frameHub = FrameHub()
    private val cameraExecutor = Dispatchers.Default.asExecutor()
    private lateinit var settingsStore: SettingsStore
    private lateinit var tcpServer: TcpTransportServer
    private lateinit var bluetoothServer: BluetoothTransportServer
    private lateinit var discovery: DiscoveryAdvertiser
    private var cameraProvider: ProcessCameraProvider? = null
    private var settingsJob: Job? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var wifiLock: WifiManager.WifiLock? = null
    private val lastFrameNanos = AtomicLong(0)

    override fun onCreate() {
        super.onCreate()
        settingsStore = SettingsStore(this)
        tcpServer = TcpTransportServer(lifecycleScope, frameHub, settingsStore)
        bluetoothServer = BluetoothTransportServer(this, lifecycleScope, frameHub, settingsStore)
        discovery = DiscoveryAdvertiser(this, lifecycleScope)
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, notification())
        acquireLocks()
        tcpServer.start()
        bluetoothServer.start()
        discovery.start()
        ServiceStatus.update { it.copy(running = true, lastError = null) }
        observeCameraConfiguration()
    }

    override fun onBind(intent: Intent): IBinder? {
        super.onBind(intent)
        return null
    }

    override fun onDestroy() {
        settingsJob?.cancel()
        cameraProvider?.unbindAll()
        discovery.stop()
        bluetoothServer.stop()
        tcpServer.stop()
        wifiLock?.takeIf { it.isHeld }?.release()
        wakeLock?.takeIf { it.isHeld }?.release()
        ServiceStatus.update { it.copy(running = false, wifiClients = 0, bluetoothClients = 0) }
        super.onDestroy()
    }

    private fun observeCameraConfiguration() {
        settingsJob = lifecycleScope.launch {
            settingsStore.settings
                .map { Triple(it.width, it.height, it.lens) }
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
                val analysis = ImageAnalysis.Builder()
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
                    .setOutputImageFormat(ImageAnalysis.OUTPUT_IMAGE_FORMAT_YUV_420_888)
                    .build()
                analysis.setAnalyzer(cameraExecutor, ::analyze)
                val selector = if (settings.lens == "front") {
                    CameraSelector.DEFAULT_FRONT_CAMERA
                } else {
                    CameraSelector.DEFAULT_BACK_CAMERA
                }
                provider.bindToLifecycle(this, selector, analysis)
            } catch (error: Exception) {
                ServiceStatus.update { it.copy(lastError = "Camera: ${error.message}") }
            }
        }, ContextCompat.getMainExecutor(this))
    }

    private fun analyze(image: ImageProxy) {
        try {
            val settings = settingsStore.settings.value
            val now = System.nanoTime()
            val minimumInterval = TimeUnit.SECONDS.toNanos(1) / settings.fps
            val previous = lastFrameNanos.get()
            if (now - previous < minimumInterval || !lastFrameNanos.compareAndSet(previous, now)) return
            val jpeg = YuvToJpeg.encode(image, settings.jpegQuality)
            frameHub.publish(
                EncodedFrame(
                    image.width,
                    image.height,
                    image.imageInfo.rotationDegrees,
                    jpeg,
                    System.currentTimeMillis() * 1_000,
                ),
            )
            ServiceStatus.update { it.copy(framesEncoded = it.framesEncoded + 1) }
        } catch (error: Exception) {
            ServiceStatus.update { it.copy(lastError = "Encoder: ${error.message}") }
        } finally {
            image.close()
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
    }

    companion object {
        private const val CHANNEL_ID = "streaming"
        private const val NOTIFICATION_ID = 17890
    }
}
