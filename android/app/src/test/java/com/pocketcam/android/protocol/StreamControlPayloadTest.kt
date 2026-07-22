package com.pocketcam.android.protocol

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class StreamControlPayloadTest {
    @Test
    fun decodesActiveAndStandbyRoutes() {
        assertEquals(true, StreamControlPayload.decode("{\"stream\":true}".toByteArray()))
        assertEquals(false, StreamControlPayload.decode("{\"stream\":false}".toByteArray()))
    }

    @Test
    fun ignoresStatusMessagesWithoutStreamControl() {
        assertNull(StreamControlPayload.decode("{\"cameraFps\":60}".toByteArray()))
        assertNull(StreamControlPayload.decode("invalid".toByteArray()))
    }
}
