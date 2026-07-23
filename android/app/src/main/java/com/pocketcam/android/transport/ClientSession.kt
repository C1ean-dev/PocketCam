package com.pocketcam.android.transport

import android.os.Build
import com.pocketcam.android.protocol.PerformanceStatusPayload
import com.pocketcam.android.protocol.StreamControlPayload
import com.pocketcam.android.protocol.WireProtocol
import com.pocketcam.android.settings.SettingsStore
import com.pocketcam.android.settings.SettingsPayload
import com.pocketcam.android.stream.FrameHub
import com.pocketcam.android.stream.ServiceStatus
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.json.JSONObject
import java.io.Closeable
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.util.UUID
import java.util.concurrent.atomic.AtomicInteger

class ClientSession(
    private val input: InputStream,
    private val output: OutputStream,
    private val closeable: Closeable,
    private val frameHub: FrameHub,
    private val settingsStore: SettingsStore,
    private val scope: CoroutineScope,
    private val appVersion: String,
) {
    private val sequence = AtomicInteger(1)
    // Backup routes stay connected for seamless failover, but must not wake up
    // for every encoded frame while they are disabled by the desktop arbiter.
    private val streamFrames = MutableStateFlow(true)

    suspend fun run() {
        var writers: List<Job> = emptyList()
        try {
            send(WireProtocol.Type.HELLO, helloPayload())
            writers = listOf(
                launchWriter {
                    streamFrames.collectLatest { enabled ->
                        if (enabled) {
                            frameHub.frames.collectLatest { frame ->
                                sendFrame(frame)
                            }
                        }
                    }
                },
                launchWriter {
                    settingsStore.settings.collect { settings ->
                        send(WireProtocol.Type.SETTINGS, SettingsPayload.encode(settings))
                    }
                },
                launchWriter {
                    ServiceStatus.value.collect { status ->
                        send(WireProtocol.Type.STATUS, PerformanceStatusPayload.encode(status))
                    }
                },
            )

            while (scope.isActive) {
                val message = WireProtocol.read(input)
                when (message.type) {
                    WireProtocol.Type.PING -> send(WireProtocol.Type.PONG, message.payload)
                    WireProtocol.Type.SETTINGS -> applySettings(message.payload)
                    WireProtocol.Type.STATUS -> applyStreamControl(message.payload)
                    else -> Unit
                }
            }
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (_: IOException) {
            // EOF and connection resets are normal when a route is probed, replaced or unplugged.
        } finally {
            runCatching { closeable.close() }
            writers.forEach(Job::cancel)
            writers.forEach { writer ->
                try {
                    writer.join()
                } catch (_: CancellationException) {
                    // The parent service is already stopping.
                }
            }
        }
    }

    private fun launchWriter(block: suspend () -> Unit): Job = scope.launch(Dispatchers.IO) {
        try {
            block()
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (_: IOException) {
            // Closing the socket wakes the reader and ends only this client session.
            runCatching { closeable.close() }
        } catch (_: Exception) {
            runCatching { closeable.close() }
        }
    }

    private fun helloPayload(): ByteArray = JSONObject()
        .put("deviceId", deviceId())
        .put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}".trim())
        .put("appVersion", appVersion)
        .put("capabilities", org.json.JSONArray(listOf("jpeg", "settings", "settings-sync", "route-control", "wifi", "usb-adb", "bluetooth-rfcomm")))
        .toString()
        .toByteArray(Charsets.UTF_8)

    private fun applySettings(payload: ByteArray) {
        try {
            val applied = SettingsPayload.decode(payload, settingsStore.settings.value)
            settingsStore.update(applied)
            // Direct response is the acknowledgement even when the requested state was unchanged.
            send(WireProtocol.Type.SETTINGS, SettingsPayload.encode(applied))
        } catch (error: Exception) {
            val response = JSONObject().put("message", "Configurações inválidas: ${error.message}")
            send(WireProtocol.Type.ERROR, response.toString().toByteArray(Charsets.UTF_8))
        }
    }

    private fun applyStreamControl(payload: ByteArray) {
        StreamControlPayload.decode(payload)?.let { enabled -> streamFrames.value = enabled }
    }

    private fun send(type: WireProtocol.Type, payload: ByteArray, timestamp: Long = System.currentTimeMillis() * 1_000) {
        synchronized(output) {
            WireProtocol.write(
                output,
                WireProtocol.Message(
                    type = type,
                    sequence = sequence.getAndIncrement(),
                    timestampMicros = timestamp,
                    payload = payload,
                ),
            )
        }
    }

    private fun sendFrame(frame: com.pocketcam.android.stream.EncodedFrame) {
        if (!streamFrames.value) return
        synchronized(output) {
            if (!streamFrames.value) return
            WireProtocol.writeFrame(
                output = output,
                sequence = sequence.getAndIncrement(),
                timestampMicros = frame.capturedAtMicros,
                width = frame.width,
                height = frame.height,
                rotation = frame.rotation,
                jpeg = frame.jpeg,
                jpegLength = frame.jpegLength,
            )
            ServiceStatus.recordTransmittedFrame()
        }
    }

    companion object {
        private fun deviceId(): String = UUID.nameUUIDFromBytes(
            "${Build.MANUFACTURER}/${Build.MODEL}/${Build.DEVICE}".toByteArray(),
        ).toString()
    }
}
