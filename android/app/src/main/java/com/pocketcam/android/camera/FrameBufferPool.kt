package com.pocketcam.android.camera

import java.util.concurrent.ArrayBlockingQueue

internal class FrameBufferPool(
    val bufferSize: Int,
    capacity: Int,
) {
    private val buffers = ArrayBlockingQueue<ByteArray>(capacity)

    init {
        require(bufferSize > 0)
        require(capacity > 0)
        repeat(capacity) { buffers.add(ByteArray(bufferSize)) }
    }

    fun acquire(): ByteArray? = buffers.poll()

    fun release(buffer: ByteArray) {
        if (buffer.size == bufferSize) buffers.offer(buffer)
    }
}
