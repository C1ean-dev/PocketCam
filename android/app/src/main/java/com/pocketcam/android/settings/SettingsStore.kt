package com.pocketcam.android.settings

import android.content.Context
import android.content.SharedPreferences
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class SettingsStore(context: Context) {
    private val repository = sharedRepository(context.applicationContext)
    val settings: StateFlow<StreamSettings> = repository.settings

    fun update(value: StreamSettings) = repository.update(value)

    companion object {
        @Volatile
        private var shared: SettingsRepository? = null

        private fun sharedRepository(context: Context): SettingsRepository = shared ?: synchronized(this) {
            shared ?: SettingsRepository(context).also { shared = it }
        }
    }
}

private class SettingsRepository(context: Context) {
    private val preferences = context.getSharedPreferences("pocketcam", Context.MODE_PRIVATE)
    private val mutableSettings = MutableStateFlow(read())
    private val preferenceListener = SharedPreferences.OnSharedPreferenceChangeListener { _, _ -> refresh() }
    val settings: StateFlow<StreamSettings> = mutableSettings.asStateFlow()

    init {
        // Keep one strong, process-wide listener so Activity, foreground service and every
        // transport session always observe the same StateFlow immediately.
        preferences.registerOnSharedPreferenceChangeListener(preferenceListener)
    }

    @Synchronized
    fun update(value: StreamSettings) {
        val validated = value.validated()
        if (mutableSettings.value == validated) return
        mutableSettings.value = validated
        preferences.edit()
            .putInt("width", validated.width)
            .putInt("height", validated.height)
            .putInt("fps", validated.fps)
            .putInt("jpegQuality", validated.jpegQuality)
            .putString("lens", validated.lens)
            .apply()
    }

    @Synchronized
    private fun refresh() {
        val persisted = read().validated()
        if (mutableSettings.value != persisted) mutableSettings.value = persisted
    }

    private fun read() = StreamSettings(
        width = preferences.getInt("width", 1280),
        height = preferences.getInt("height", 720),
        fps = preferences.getInt("fps", 20),
        jpegQuality = preferences.getInt("jpegQuality", 80),
        lens = preferences.getString("lens", "back") ?: "back",
    )
}
