package com.pocketcam.android.camera

import java.util.concurrent.TimeUnit

internal class FrameRateLimiter {
    private var nextFrameNanos = Long.MIN_VALUE

    @Synchronized
    fun reset() {
        nextFrameNanos = Long.MIN_VALUE
    }

    @Synchronized
    fun shouldProcess(nowNanos: Long, framesPerSecond: Int): Boolean {
        require(framesPerSecond > 0)
        val interval = TimeUnit.SECONDS.toNanos(1) / framesPerSecond
        if (nextFrameNanos == Long.MIN_VALUE) {
            nextFrameNanos = nowNanos + interval
            return true
        }

        // Camera timestamps naturally jitter around the requested cadence. A quarter-frame
        // tolerance prevents a nominal 60 FPS source from being cut to 30 FPS whenever one
        // callback arrives a fraction of a millisecond early.
        if (nowNanos + interval / 4 < nextFrameNanos) return false

        nextFrameNanos = if (nowNanos - nextFrameNanos > interval * 2) {
            nowNanos + interval
        } else {
            nextFrameNanos + interval
        }
        return true
    }
}
