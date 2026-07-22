package com.pocketcam.android.stream

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import java.util.concurrent.atomic.AtomicLong

data class StreamingStatus(
    val running: Boolean = false,
    val wifiClients: Int = 0,
    val bluetoothClients: Int = 0,
    val targetFps: Int = 0,
    val cameraFps: Double = 0.0,
    val encodedFps: Double = 0.0,
    val transmittedFps: Double = 0.0,
    val droppedFps: Double = 0.0,
    val lastError: String? = null,
)

object ServiceStatus {
    private val mutable = MutableStateFlow(StreamingStatus())
    private val transmittedFrames = AtomicLong()
    val value = mutable.asStateFlow()

    fun update(block: (StreamingStatus) -> StreamingStatus) {
        mutable.update(block)
    }

    fun recordTransmittedFrame() = transmittedFrames.incrementAndGet()

    fun takeTransmittedFrames(): Long = transmittedFrames.getAndSet(0)
}
