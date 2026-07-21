package com.pocketcam.android.usb

object UsbDebuggingPromptPolicy {
    fun shouldPrompt(cableConnected: Boolean, debuggingEnabled: Boolean, dismissed: Boolean): Boolean =
        cableConnected && !debuggingEnabled && !dismissed
}
