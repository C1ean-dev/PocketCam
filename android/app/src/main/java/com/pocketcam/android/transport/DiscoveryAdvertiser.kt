package com.pocketcam.android.transport

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.Build
import com.pocketcam.android.protocol.WireProtocol
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.NetworkInterface
import java.net.SocketTimeoutException
import java.util.UUID

class DiscoveryAdvertiser(
    private val context: Context,
    private val scope: CoroutineScope,
    private val appVersion: String,
) {
    private val nsdManager = context.getSystemService(NsdManager::class.java)
    private var registration: NsdManager.RegistrationListener? = null
    private var beaconJob: Job? = null
    private var probeJob: Job? = null
    private var socket: DatagramSocket? = null

    fun start() {
        if (registration != null) return
        val listener = object : NsdManager.RegistrationListener {
            override fun onServiceRegistered(serviceInfo: NsdServiceInfo) = Unit
            override fun onRegistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) = Unit
            override fun onServiceUnregistered(serviceInfo: NsdServiceInfo) = Unit
            override fun onUnregistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) = Unit
        }
        val info = NsdServiceInfo().apply {
            serviceName = "PocketCam-${Build.MODEL}"
            serviceType = "_pocketcam._tcp."
            port = WireProtocol.TCP_PORT
            setAttribute("id", deviceId())
            setAttribute("v", "1")
        }
        nsdManager.registerService(info, NsdManager.PROTOCOL_DNS_SD, listener)
        registration = listener

        val discoverySocket = DatagramSocket(null).apply {
            reuseAddress = true
            broadcast = true
            soTimeout = 1_000
            bind(InetSocketAddress(WireProtocol.DISCOVERY_PORT))
        }
        socket = discoverySocket
        beaconJob = scope.launch(Dispatchers.IO) {
            while (isActive) {
                discoveryTargets().forEach { target ->
                    sendBeacon(discoverySocket, target, WireProtocol.DISCOVERY_PORT)
                }
                delay(1_500)
            }
        }
        probeJob = scope.launch(Dispatchers.IO) {
            val buffer = ByteArray(256)
            while (isActive) {
                val packet = DatagramPacket(buffer, buffer.size)
                try {
                    discoverySocket.receive(packet)
                    val message = packet.data.decodeToString(0, packet.length)
                    if (message == WireProtocol.DISCOVERY_PROBE) {
                        sendBeacon(discoverySocket, packet.address, packet.port)
                    }
                } catch (_: SocketTimeoutException) {
                    // Re-check coroutine cancellation once per second.
                } catch (error: java.net.SocketException) {
                    if (isActive) throw error
                }
            }
        }
    }

    fun stop() {
        beaconJob?.cancel()
        beaconJob = null
        probeJob?.cancel()
        probeJob = null
        socket?.close()
        socket = null
        registration?.let { runCatching { nsdManager.unregisterService(it) } }
        registration = null
    }

    private fun beaconPayload(): ByteArray = JSONObject()
        .put("magic", "PCM1")
        .put("version", 1)
        .put("deviceId", deviceId())
        .put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}".trim())
        .put("port", WireProtocol.TCP_PORT)
        .put("appVersion", appVersion)
        .toString()
        .toByteArray(Charsets.UTF_8)

    private fun sendBeacon(socket: DatagramSocket, address: InetAddress, port: Int) {
        val bytes = beaconPayload()
        runCatching {
            synchronized(socket) {
                socket.send(DatagramPacket(bytes, bytes.size, address, port))
            }
        }
    }

    private fun discoveryTargets(): Set<InetAddress> {
        val targets = mutableSetOf(
            InetAddress.getByName(WireProtocol.DISCOVERY_GROUP),
            InetAddress.getByName("255.255.255.255"),
        )
        runCatching {
            val interfaces = NetworkInterface.getNetworkInterfaces()
            while (interfaces.hasMoreElements()) {
                val networkInterface = interfaces.nextElement()
                if (!networkInterface.isUp || networkInterface.isLoopback) continue
                networkInterface.interfaceAddresses.mapNotNullTo(targets) { it.broadcast }
            }
        }
        return targets
    }

    private fun deviceId(): String = UUID.nameUUIDFromBytes(
        "${Build.MANUFACTURER}/${Build.MODEL}/${Build.DEVICE}".toByteArray(),
    ).toString()
}
