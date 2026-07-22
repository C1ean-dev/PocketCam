package com.pocketcam.android.camera

import java.util.concurrent.TimeUnit
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class FrameRateLimiterTest {
    @Test
    fun preservesSixtyFpsWhenCameraCallbacksJitter() {
        val limiter = FrameRateLimiter()
        val interval = TimeUnit.SECONDS.toNanos(1) / 60
        var accepted = 0

        repeat(60) { index ->
            val jitter = if (index % 2 == 0) -300_000 else 300_000
            if (limiter.shouldProcess(index * interval + jitter, 60)) accepted++
        }

        assertEquals(60, accepted)
    }

    @Test
    fun rateLimitsThirtyFpsInputToApproximatelyTwenty() {
        val limiter = FrameRateLimiter()
        val cameraInterval = TimeUnit.SECONDS.toNanos(1) / 30
        var accepted = 0

        repeat(90) { index ->
            if (limiter.shouldProcess(index * cameraInterval, 20)) accepted++
        }

        assertTrue("accepted=$accepted", accepted in 59..61)
    }

    @Test
    fun resetAcceptsTheNextFrameImmediately() {
        val limiter = FrameRateLimiter()
        assertTrue(limiter.shouldProcess(1_000_000, 60))
        limiter.reset()
        assertTrue(limiter.shouldProcess(1_000_001, 60))
    }
}
