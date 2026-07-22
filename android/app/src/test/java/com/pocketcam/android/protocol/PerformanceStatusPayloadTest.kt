package com.pocketcam.android.protocol

import com.pocketcam.android.stream.StreamingStatus
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Test

class PerformanceStatusPayloadTest {
    @Test
    fun encodesEveryPipelineStage() {
        val payload = PerformanceStatusPayload.encode(
            StreamingStatus(
                targetFps = 60,
                cameraFps = 59.8,
                encodedFps = 58.7,
                transmittedFps = 58.1,
                droppedFps = 1.1,
            ),
        )
        val json = JSONObject(payload.toString(Charsets.UTF_8))

        assertEquals(60, json.getInt("targetFps"))
        assertEquals(59.8, json.getDouble("cameraFps"), 0.001)
        assertEquals(58.7, json.getDouble("encodedFps"), 0.001)
        assertEquals(58.1, json.getDouble("transmittedFps"), 0.001)
        assertEquals(1.1, json.getDouble("droppedFps"), 0.001)
    }
}
