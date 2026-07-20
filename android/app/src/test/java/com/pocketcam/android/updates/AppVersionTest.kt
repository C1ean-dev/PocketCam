package com.pocketcam.android.updates

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class AppVersionTest {
    @Test
    fun parsesReleaseAndDebugVersions() {
        assertEquals(AppVersion(0, 1, 2), AppVersion.parse("v0.1.2"))
        assertEquals(AppVersion(0, 1, 2), AppVersion.parse("0.1.2-debug"))
        assertEquals(AppVersion(2, 4, 0), AppVersion.parse("V2.4"))
    }

    @Test
    fun rejectsInvalidVersion() {
        assertNull(AppVersion.parse("latest"))
        assertNull(AppVersion.parse("1"))
    }

    @Test
    fun comparesSemanticVersionComponents() {
        assertTrue(AppVersion(0, 2, 0) > AppVersion(0, 1, 9))
        assertTrue(AppVersion(1, 0, 0) > AppVersion(0, 99, 99))
    }
}
