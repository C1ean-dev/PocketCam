package com.pocketcam.android.camera

import java.io.ByteArrayOutputStream

internal data class EncodedJpeg(
    val bytes: ByteArray,
    val length: Int,
)

internal class DetachableByteArrayOutputStream(initialCapacity: Int = MIN_CAPACITY) :
    ByteArrayOutputStream(initialCapacity.coerceAtLeast(MIN_CAPACITY)) {

    fun detach(): EncodedJpeg {
        check(count > 0) { "Cannot detach an empty JPEG buffer" }
        val result = EncodedJpeg(buf, count)
        buf = ByteArray(count.coerceAtLeast(MIN_CAPACITY))
        count = 0
        return result
    }

    companion object {
        private const val MIN_CAPACITY = 16 * 1024
    }
}
