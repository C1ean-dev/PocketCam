package com.pocketcam.android.settings

import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class SettingsPayloadTest {
    @Test
    fun roundTripsEverySetting() {
        val original = StreamSettings(1920, 1080, 30, 75, "front")

        val decoded = SettingsPayload.decode(SettingsPayload.encode(original), StreamSettings())

        assertEquals(original, decoded)
    }

    @Test
    fun fillsMissingFieldsFromCurrentSettings() {
        val current = StreamSettings(1280, 720, 20, 80, "back")

        val decoded = SettingsPayload.decode("{\"fps\":30}".toByteArray(), current)

        assertEquals(current.copy(fps = 30), decoded)
    }

    @Test
    fun rejectsInvalidRemoteSettings() {
        assertThrows(IllegalArgumentException::class.java) {
            SettingsPayload.decode("{\"fps\":0}".toByteArray(), StreamSettings())
        }
    }
}
