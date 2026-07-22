package com.pocketcam.android.protocol

import org.json.JSONObject

object StreamControlPayload {
    fun decode(payload: ByteArray): Boolean? = runCatching {
        val json = JSONObject(payload.toString(Charsets.UTF_8))
        if (!json.has("stream")) null else json.getBoolean("stream")
    }.getOrNull()
}
