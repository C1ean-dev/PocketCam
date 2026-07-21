package com.pocketcam.android.stream

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

data class StreamingStatus(
    val running: Boolean = false,
    val wifiClients: Int = 0,
    val bluetoothClients: Int = 0,
    val lastError: String? = null,
)

object ServiceStatus {
    private val mutable = MutableStateFlow(StreamingStatus())
    val value = mutable.asStateFlow()

    fun update(block: (StreamingStatus) -> StreamingStatus) {
        mutable.value = block(mutable.value)
    }
}
