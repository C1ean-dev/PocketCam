package com.pocketcam.android.usb

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class UsbDebuggingPromptPolicyTest {
    @Test
    fun promptsWhenCableIsConnectedAndDebuggingIsDisabled() {
        assertTrue(UsbDebuggingPromptPolicy.shouldPrompt(cableConnected = true, debuggingEnabled = false, dismissed = false))
    }

    @Test
    fun doesNotPromptWithoutCableOrWhenAlreadyConfigured() {
        assertFalse(UsbDebuggingPromptPolicy.shouldPrompt(cableConnected = false, debuggingEnabled = false, dismissed = false))
        assertFalse(UsbDebuggingPromptPolicy.shouldPrompt(cableConnected = true, debuggingEnabled = true, dismissed = false))
    }

    @Test
    fun respectsDismissalForCurrentCableSession() {
        assertFalse(UsbDebuggingPromptPolicy.shouldPrompt(cableConnected = true, debuggingEnabled = false, dismissed = true))
    }
}
