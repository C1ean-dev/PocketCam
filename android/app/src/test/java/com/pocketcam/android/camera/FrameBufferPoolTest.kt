package com.pocketcam.android.camera

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Test

class FrameBufferPoolTest {
    @Test
    fun boundsFramesWaitingForTheEncoders() {
        val pool = FrameBufferPool(bufferSize = 16, capacity = 2)
        val first = pool.acquire()
        val second = pool.acquire()

        assertEquals(16, first?.size)
        assertEquals(16, second?.size)
        assertNull(pool.acquire())

        pool.release(first!!)
        assertSame(first, pool.acquire())
    }

    @Test
    fun ignoresBuffersFromAnOldResolution() {
        val pool = FrameBufferPool(bufferSize = 16, capacity = 1)
        pool.acquire()
        pool.release(ByteArray(32))

        assertNull(pool.acquire())
    }
}
