package com.pocketcam.android.settings

import android.content.Context
import android.content.SharedPreferences
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class SettingsStore(context: Context) {
    private val preferences = context.getSharedPreferences("pocketcam", Context.MODE_PRIVATE)
    private val mutableSettings = MutableStateFlow(read())
    private val preferenceListener = SharedPreferences.OnSharedPreferenceChangeListener { _, _ ->
        mutableSettings.value = read()
    }
    val settings: StateFlow<StreamSettings> = mutableSettings.asStateFlow()

    init {
        preferences.registerOnSharedPreferenceChangeListener(preferenceListener)
    }

    @Synchronized
    fun update(value: StreamSettings) {
        value.validated()
        preferences.edit()
            .putInt("width", value.width)
            .putInt("height", value.height)
            .putInt("fps", value.fps)
            .putInt("jpegQuality", value.jpegQuality)
            .putString("lens", value.lens)
            .apply()
        mutableSettings.value = value
    }

    private fun read() = StreamSettings(
        width = preferences.getInt("width", 1280),
        height = preferences.getInt("height", 720),
        fps = preferences.getInt("fps", 20),
        jpegQuality = preferences.getInt("jpegQuality", 80),
        lens = preferences.getString("lens", "back") ?: "back",
    )
}
