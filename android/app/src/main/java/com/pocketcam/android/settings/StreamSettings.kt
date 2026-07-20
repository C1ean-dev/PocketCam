package com.pocketcam.android.settings

data class StreamSettings(
    val width: Int = 1280,
    val height: Int = 720,
    val fps: Int = 20,
    val jpegQuality: Int = 80,
    val lens: String = "back",
) {
    fun validated(): StreamSettings {
        require(width in 160..3840)
        require(height in 120..2160)
        require(fps in 1..60)
        require(jpegQuality in 20..100)
        require(lens == "front" || lens == "back")
        return this
    }
}

