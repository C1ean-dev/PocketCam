package com.pocketcam.android.protocol

import com.pocketcam.android.stream.StreamingStatus
import org.json.JSONObject

object PerformanceStatusPayload {
    fun encode(status: StreamingStatus): ByteArray = JSONObject()
        .put("targetFps", status.targetFps)
        .put("cameraFps", status.cameraFps)
        .put("encodedFps", status.encodedFps)
        .put("transmittedFps", status.transmittedFps)
        .put("droppedFps", status.droppedFps)
        .toString()
        .toByteArray(Charsets.UTF_8)
}
