package com.pocketcam.android.camera

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test

class DetachableByteArrayOutputStreamTest {
    @Test
    fun detachedBufferIsNotMutatedByTheNextFrame() {
        val output = DetachableByteArrayOutputStream()
        output.write(byteArrayOf(1, 2, 3))
        val first = output.detach()

        output.write(byteArrayOf(4, 5))
        val second = output.detach()

        assertEquals(3, first.length)
        assertArrayEquals(byteArrayOf(1, 2, 3), first.bytes.copyOf(first.length))
        assertEquals(2, second.length)
        assertArrayEquals(byteArrayOf(4, 5), second.bytes.copyOf(second.length))
    }
}
