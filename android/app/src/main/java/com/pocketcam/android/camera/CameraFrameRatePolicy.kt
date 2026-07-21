package com.pocketcam.android.camera

import kotlin.math.abs

data class FrameRateRange(val lower: Int, val upper: Int) {
    init {
        require(lower > 0 && upper >= lower)
    }
}

object CameraFrameRatePolicy {
    fun select(available: Collection<FrameRateRange>, requestedFps: Int): FrameRateRange? = available
        .minWithOrNull(
            compareBy<FrameRateRange>(
                { if (requestedFps in it.lower..it.upper) 0 else 1 },
                { abs(it.upper - requestedFps) },
                { it.upper - it.lower },
                { abs(it.lower - requestedFps) },
            ),
        )
}
