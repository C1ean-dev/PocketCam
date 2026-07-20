package com.pocketcam.android.transport

import com.pocketcam.android.protocol.WireProtocol
import com.pocketcam.android.settings.SettingsStore
import com.pocketcam.android.stream.FrameHub
import com.pocketcam.android.stream.ServiceStatus
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.net.SocketException
import java.util.Collections

class TcpTransportServer(
    private val scope: CoroutineScope,
    private val frameHub: FrameHub,
    private val settingsStore: SettingsStore,
) {
    private var serverSocket: ServerSocket? = null
    private var acceptJob: Job? = null
    private val sessions = Collections.synchronizedSet(mutableSetOf<Job>())
    private val sockets = Collections.synchronizedSet(mutableSetOf<Socket>())

    fun start() {
        if (acceptJob != null) return
        acceptJob = scope.launch(Dispatchers.IO) {
            val server = ServerSocket().also {
                it.reuseAddress = true
                it.bind(InetSocketAddress(WireProtocol.TCP_PORT))
                serverSocket = it
            }
            try {
                while (isActive) {
                    val socket = server.accept().apply {
                        tcpNoDelay = true
                        keepAlive = true
                    }
                    sockets += socket
                    val job = launch {
                        ServiceStatus.update { it.copy(wifiClients = it.wifiClients + 1) }
                        try {
                            ClientSession(
                                socket.getInputStream(), socket.getOutputStream(), socket,
                                frameHub, settingsStore, this,
                            ).run()
                        } finally {
                            sockets -= socket
                            ServiceStatus.update { it.copy(wifiClients = (it.wifiClients - 1).coerceAtLeast(0)) }
                        }
                    }
                    sessions += job
                    job.invokeOnCompletion { sessions -= job }
                }
            } catch (error: SocketException) {
                if (isActive) ServiceStatus.update { it.copy(lastError = error.message) }
            } finally {
                server.close()
            }
        }
    }

    fun stop() {
        serverSocket?.close()
        acceptJob?.cancel()
        acceptJob = null
        sockets.toList().forEach { runCatching { it.close() } }
        sockets.clear()
        sessions.toList().forEach(Job::cancel)
        sessions.clear()
    }
}
