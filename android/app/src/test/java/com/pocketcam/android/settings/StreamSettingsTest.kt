package com.pocketcam.android.settings

import org.junit.Assert.assertEquals
import org.junit.Test

class StreamSettingsTest {
    @Test
    fun supportedSettingsAreAccepted() {
        val settings = StreamSettings(1920, 1080, 30, 90, "front")

        assertEquals(settings, settings.validated())
    }

    @Test(expected = IllegalArgumentException::class)
    fun invalidFpsIsRejected() {
        StreamSettings(fps = 0).validated()
    }

    @Test(expected = IllegalArgumentException::class)
    fun invalidLensIsRejected() {
        StreamSettings(lens = "external").validated()
    }
}
