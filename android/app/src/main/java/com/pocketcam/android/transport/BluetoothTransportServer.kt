package com.pocketcam.android.transport

import android.Manifest
import android.bluetooth.BluetoothManager
import android.bluetooth.BluetoothServerSocket
import android.bluetooth.BluetoothSocket
import android.content.Context
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import com.pocketcam.android.protocol.WireProtocol
import com.pocketcam.android.settings.SettingsStore
import com.pocketcam.android.stream.FrameHub
import com.pocketcam.android.stream.ServiceStatus
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.IOException
import java.util.Collections
import java.util.UUID

class BluetoothTransportServer(
    private val context: Context,
    private val scope: CoroutineScope,
    private val frameHub: FrameHub,
    private val settingsStore: SettingsStore,
    private val appVersion: String,
) {
    private var serverSocket: BluetoothServerSocket? = null
    private var acceptJob: Job? = null
    private val sessions = Collections.synchronizedSet(mutableSetOf<Job>())
    private val sockets = Collections.synchronizedSet(mutableSetOf<BluetoothSocket>())

    fun start() {
        if (acceptJob != null || !hasPermission()) return
        val adapter = context.getSystemService(BluetoothManager::class.java)?.adapter ?: return
        if (!adapter.isEnabled) return
        acceptJob = scope.launch(Dispatchers.IO) {
            try {
                @Suppress("MissingPermission")
                val server = adapter.listenUsingRfcommWithServiceRecord(
                    "PocketCam",
                    UUID.fromString(WireProtocol.BLUETOOTH_UUID),
                ).also { serverSocket = it }
                while (isActive) {
                    @Suppress("MissingPermission")
                    val socket = server.accept()
                    sockets += socket
                    val job = launch {
                        ServiceStatus.update { it.copy(bluetoothClients = it.bluetoothClients + 1) }
                        try {
                            ClientSession(
                                socket.inputStream, socket.outputStream, socket,
                                frameHub, settingsStore, this, appVersion,
                            ).run()
                        } catch (cancelled: CancellationException) {
                            throw cancelled
                        } catch (error: Exception) {
                            ServiceStatus.update { it.copy(lastError = "Sessão Bluetooth: ${error.message}") }
                        } finally {
                            sockets -= socket
                            ServiceStatus.update {
                                it.copy(bluetoothClients = (it.bluetoothClients - 1).coerceAtLeast(0))
                            }
                        }
                    }
                    sessions += job
                    job.invokeOnCompletion { sessions -= job }
                }
            } catch (error: IOException) {
                if (isActive) ServiceStatus.update { it.copy(lastError = error.message) }
            }
        }
    }

    fun stop() {
        try {
            serverSocket?.close()
        } catch (_: IOException) {
            // Already closed.
        }
        acceptJob?.cancel()
        acceptJob = null
        sockets.toList().forEach { runCatching { it.close() } }
        sockets.clear()
        sessions.toList().forEach(Job::cancel)
        sessions.clear()
    }

    private fun hasPermission(): Boolean = android.os.Build.VERSION.SDK_INT < 31 ||
        ContextCompat.checkSelfPermission(context, Manifest.permission.BLUETOOTH_CONNECT) == PackageManager.PERMISSION_GRANTED
}
