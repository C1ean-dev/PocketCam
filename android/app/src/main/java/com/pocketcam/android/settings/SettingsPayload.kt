package com.pocketcam.android.settings

import org.json.JSONObject

object SettingsPayload {
    fun encode(settings: StreamSettings): ByteArray = JSONObject()
        .put("width", settings.width)
        .put("height", settings.height)
        .put("fps", settings.fps)
        .put("jpegQuality", settings.jpegQuality)
        .put("lens", settings.lens)
        .toString()
        .toByteArray(Charsets.UTF_8)

    fun decode(payload: ByteArray, fallback: StreamSettings): StreamSettings {
        val json = JSONObject(payload.toString(Charsets.UTF_8))
        return StreamSettings(
            width = json.optInt("width", fallback.width),
            height = json.optInt("height", fallback.height),
            fps = json.optInt("fps", fallback.fps),
            jpegQuality = json.optInt("jpegQuality", fallback.jpegQuality),
            lens = json.optString("lens", fallback.lens),
        ).validated()
    }
}
