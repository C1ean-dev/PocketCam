package com.pocketcam.android.transport

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.Build
import com.pocketcam.android.BuildConfig
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
import java.util.UUID

class DiscoveryAdvertiser(private val context: Context, private val scope: CoroutineScope) {
    private val nsdManager = context.getSystemService(NsdManager::class.java)
    private var registration: NsdManager.RegistrationListener? = null
    private var beaconJob: Job? = null

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

        beaconJob = scope.launch(Dispatchers.IO) {
            DatagramSocket().use { socket ->
                socket.broadcast = true
                val targets = listOf(
                    InetAddress.getByName(WireProtocol.DISCOVERY_GROUP),
                    InetAddress.getByName("255.255.255.255"),
                )
                while (isActive) {
                    val bytes = beaconPayload()
                    targets.forEach { target ->
                        runCatching {
                            socket.send(DatagramPacket(bytes, bytes.size, target, WireProtocol.DISCOVERY_PORT))
                        }
                    }
                    delay(1_500)
                }
            }
        }
    }

    fun stop() {
        beaconJob?.cancel()
        beaconJob = null
        registration?.let { runCatching { nsdManager.unregisterService(it) } }
        registration = null
    }

    private fun beaconPayload(): ByteArray = JSONObject()
        .put("magic", "PCM1")
        .put("version", 1)
        .put("deviceId", deviceId())
        .put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}".trim())
        .put("port", WireProtocol.TCP_PORT)
        .put("appVersion", BuildConfig.VERSION_NAME)
        .toString()
        .toByteArray(Charsets.UTF_8)

    private fun deviceId(): String = UUID.nameUUIDFromBytes(
        "${Build.MANUFACTURER}/${Build.MODEL}/${Build.DEVICE}".toByteArray(),
    ).toString()
}

