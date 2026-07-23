package com.pocketcam.android.camera

import kotlin.math.abs

data class FrameRateRange(val lower: Int, val upper: Int) {
    init {
        require(lower > 0 && upper >= lower)
    }
}

data class FrameRatePlan(
    val requestedFps: Int,
    val cameraRange: FrameRateRange?,
)

object CameraFrameRatePolicy {
    fun plan(available: Collection<FrameRateRange>, requestedFps: Int): FrameRatePlan =
        FrameRatePlan(requestedFps, select(available, requestedFps))

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
