package com.pocketcam.android.settings

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SettingsStoreInstrumentedTest {
    @Test
    fun activityAndServiceInstancesShareOneImmediateState() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val activityStore = SettingsStore(context)
        val serviceStore = SettingsStore(context)
        val original = activityStore.settings.value
        val changed = StreamSettings(640, 480, 60, 40, "front")

        try {
            activityStore.update(changed)
            assertEquals(changed, serviceStore.settings.value)

            serviceStore.update(original)
            assertEquals(original, activityStore.settings.value)
        } finally {
            activityStore.update(original)
        }
    }
}
