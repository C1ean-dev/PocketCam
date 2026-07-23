package com.pocketcam.android.camera

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class CameraFrameRatePolicyTest {
    @Test
    fun prefersFixedRangeAtRequestedFrameRate() {
        val selected = CameraFrameRatePolicy.select(
            listOf(FrameRateRange(15, 30), FrameRateRange(30, 30), FrameRateRange(60, 60)),
            30,
        )

        assertEquals(FrameRateRange(30, 30), selected)
    }

    @Test
    fun choosesContainingRangeForIntermediateFrameRate() {
        val selected = CameraFrameRatePolicy.select(
            listOf(FrameRateRange(15, 30), FrameRateRange(30, 30)),
            20,
        )

        assertEquals(FrameRateRange(15, 30), selected)
    }

    @Test
    fun handlesCameraWithoutReportedRanges() {
        assertNull(CameraFrameRatePolicy.select(emptyList(), 30))
    }

    @Test
    fun keepsSixtyAsTargetWhenCameraRangeFallsBackToThirty() {
        val plan = CameraFrameRatePolicy.plan(
            listOf(FrameRateRange(15, 30), FrameRateRange(30, 30)),
            60,
        )

        assertEquals(60, plan.requestedFps)
        assertEquals(FrameRateRange(30, 30), plan.cameraRange)
    }
}
