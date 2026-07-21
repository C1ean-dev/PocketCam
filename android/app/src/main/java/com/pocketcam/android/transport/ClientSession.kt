package com.pocketcam.android.transport

import android.os.Build
import com.pocketcam.android.protocol.FramePayload
import com.pocketcam.android.protocol.WireProtocol
import com.pocketcam.android.settings.SettingsStore
import com.pocketcam.android.settings.StreamSettings
import com.pocketcam.android.stream.FrameHub
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.collectLatest
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

    suspend fun run() {
        var writer: Job? = null
        try {
            send(WireProtocol.Type.HELLO, helloPayload())
            writer = scope.launch(Dispatchers.IO) {
                try {
                    frameHub.frames.collectLatest { frame ->
                        val payload = FramePayload(frame.width, frame.height, frame.rotation, frame.jpeg).encode()
                        send(WireProtocol.Type.FRAME, payload, frame.capturedAtMicros)
                    }
                } catch (cancelled: CancellationException) {
                    throw cancelled
                } catch (_: IOException) {
                    // Closing the socket below wakes the reader and ends only this client session.
                    runCatching { closeable.close() }
                } catch (_: Exception) {
                    runCatching { closeable.close() }
                }
            }

            while (scope.isActive) {
                val message = WireProtocol.read(input)
                when (message.type) {
                    WireProtocol.Type.PING -> send(WireProtocol.Type.PONG, message.payload)
                    WireProtocol.Type.SETTINGS -> applySettings(message.payload)
                    else -> Unit
                }
            }
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (_: IOException) {
            // EOF and connection resets are normal when a route is probed, replaced or unplugged.
        } finally {
            runCatching { closeable.close() }
            writer?.cancel()
            try {
                writer?.join()
            } catch (_: CancellationException) {
                // The parent service is already stopping.
            }
        }
    }

    private fun helloPayload(): ByteArray = JSONObject()
        .put("deviceId", deviceId())
        .put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}".trim())
        .put("appVersion", appVersion)
        .put("capabilities", org.json.JSONArray(listOf("jpeg", "settings", "wifi", "usb-adb", "bluetooth-rfcomm")))
        .toString()
        .toByteArray(Charsets.UTF_8)

    private fun applySettings(payload: ByteArray) {
        val json = JSONObject(payload.toString(Charsets.UTF_8))
        val current = settingsStore.settings.value
        settingsStore.update(
            StreamSettings(
                width = json.optInt("width", current.width),
                height = json.optInt("height", current.height),
                fps = json.optInt("fps", current.fps),
                jpegQuality = json.optInt("jpegQuality", current.jpegQuality),
                lens = json.optString("lens", current.lens),
            ),
        )
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

    companion object {
        private fun deviceId(): String = UUID.nameUUIDFromBytes(
            "${Build.MANUFACTURER}/${Build.MODEL}/${Build.DEVICE}".toByteArray(),
        ).toString()
    }
}
