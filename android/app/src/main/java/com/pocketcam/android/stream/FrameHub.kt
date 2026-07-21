package com.pocketcam.android.stream

import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

data class EncodedFrame(
    val payload: ByteArray,
    val capturedAtMicros: Long,
)

class FrameHub {
    private val mutableFrames = MutableSharedFlow<EncodedFrame>(
        replay = 1,
        extraBufferCapacity = 1,
        onBufferOverflow = BufferOverflow.DROP_OLDEST,
    )
    val frames: SharedFlow<EncodedFrame> = mutableFrames.asSharedFlow()

    fun publish(frame: EncodedFrame) {
        mutableFrames.tryEmit(frame)
    }
}
