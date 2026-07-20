package com.pocketcam.android.protocol

import java.nio.ByteBuffer
import java.nio.ByteOrder

data class FramePayload(
    val width: Int,
    val height: Int,
    val rotation: Int,
    val jpeg: ByteArray,
) {
    fun encode(): ByteArray {
        require(width in 1..7680 && height in 1..4320)
        require(rotation in setOf(0, 90, 180, 270))
        require(jpeg.isNotEmpty())
        return ByteBuffer.allocate(8 + jpeg.size).order(ByteOrder.LITTLE_ENDIAN).apply {
            putShort(width.toShort())
            putShort(height.toShort())
            putShort(rotation.toShort())
            put(1) // JPEG
            put(0)
            put(jpeg)
        }.array()
    }
}

