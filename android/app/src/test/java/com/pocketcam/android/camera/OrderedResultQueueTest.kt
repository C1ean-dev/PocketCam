package com.pocketcam.android.camera

import org.junit.Assert.assertEquals
import org.junit.Test

class OrderedResultQueueTest {
    @Test
    fun publishesParallelResultsInCaptureOrder() {
        val queue = OrderedResultQueue<String>()
        queue.reset(generation = 7, nextSequence = 10)

        assertEquals(emptyList<String>(), queue.complete(7, 11, "second"))
        assertEquals(listOf("first", "second"), queue.complete(7, 10, "first"))
    }

    @Test
    fun skippedFrameDoesNotBlockNewerFrames() {
        val queue = OrderedResultQueue<String>()
        queue.reset(generation = 2, nextSequence = 4)

        assertEquals(emptyList<String>(), queue.complete(2, 5, "next"))
        assertEquals(listOf("next"), queue.complete(2, 4, null))
    }

    @Test
    fun ignoresResultsFromPreviousCameraBinding() {
        val queue = OrderedResultQueue<String>()
        queue.reset(generation = 1, nextSequence = 0)
        queue.reset(generation = 2, nextSequence = 5)

        assertEquals(emptyList<String>(), queue.complete(1, 0, "old"))
        assertEquals(listOf("new"), queue.complete(2, 5, "new"))
    }
}
