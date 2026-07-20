package com.pocketcam.android

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Slider
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.pocketcam.android.camera.CameraStreamingService
import com.pocketcam.android.settings.SettingsStore
import com.pocketcam.android.stream.ServiceStatus
import com.pocketcam.android.updates.GitHubReleaseUpdateChecker
import com.pocketcam.android.updates.ReleaseUpdate
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    private lateinit var settingsStore: SettingsStore
    private val updateChecker = GitHubReleaseUpdateChecker()
    private var availableUpdate by mutableStateOf<ReleaseUpdate?>(null)
    private var updateStatus by mutableStateOf("Versão ${BuildConfig.VERSION_NAME.substringBefore('-')}")
    private var checkingForUpdates = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        settingsStore = SettingsStore(this)
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) {
            startStreaming()
        }
        setContent {
            MaterialTheme(colorScheme = pocketCamColors) {
                val permissions = requiredPermissions()
                val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { result ->
                    if (result[Manifest.permission.CAMERA] == true) startStreaming()
                }
                val hasCamera = ContextCompat.checkSelfPermission(
                    this,
                    Manifest.permission.CAMERA,
                ) == PackageManager.PERMISSION_GRANTED
                PocketCamScreen(
                    store = settingsStore,
                    hasCameraPermission = hasCamera,
                    onRequestPermission = { launcher.launch(permissions) },
                    onStart = ::startStreaming,
                    onStop = ::stopStreaming,
                    updateStatus = updateStatus,
                    availableUpdate = availableUpdate,
                    onCheckForUpdates = { checkForUpdates(userInitiated = true) },
                    onDismissUpdate = { availableUpdate = null },
                    onOpenUpdate = ::openUpdate,
                )
            }
        }
        checkForUpdates(userInitiated = false)
    }

    private fun startStreaming() {
        ContextCompat.startForegroundService(this, Intent(this, CameraStreamingService::class.java))
    }

    private fun stopStreaming() {
        stopService(Intent(this, CameraStreamingService::class.java))
    }

    private fun checkForUpdates(userInitiated: Boolean) {
        if (checkingForUpdates) {
            if (userInitiated) updateStatus = "A verificação já está em andamento…"
            return
        }

        checkingForUpdates = true
        if (userInitiated) updateStatus = "Verificando atualizações…"
        lifecycleScope.launch {
            try {
                val update = updateChecker.findUpdate()
                if (update == null) {
                    updateStatus = "Versão ${BuildConfig.VERSION_NAME.substringBefore('-')} · atualizada"
                } else {
                    updateStatus = "Versão ${update.version} disponível"
                    availableUpdate = update
                }
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Exception) {
                updateStatus = if (userInitiated) {
                    "Não foi possível consultar as releases. Tente novamente."
                } else {
                    "Versão ${BuildConfig.VERSION_NAME.substringBefore('-')}"
                }
            } finally {
                checkingForUpdates = false
            }
        }
    }

    private fun openUpdate(update: ReleaseUpdate) {
        availableUpdate = null
        val target = update.androidDownloadUri ?: update.releasePageUri
        startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(target.toString())))
    }

    private fun requiredPermissions(): Array<String> = buildList {
        add(Manifest.permission.CAMERA)
        if (Build.VERSION.SDK_INT >= 31) {
            add(Manifest.permission.BLUETOOTH_CONNECT)
            add(Manifest.permission.BLUETOOTH_ADVERTISE)
        }
        if (Build.VERSION.SDK_INT >= 33) add(Manifest.permission.POST_NOTIFICATIONS)
    }.toTypedArray()
}

@Composable
private fun PocketCamScreen(
    store: SettingsStore,
    hasCameraPermission: Boolean,
    onRequestPermission: () -> Unit,
    onStart: () -> Unit,
    onStop: () -> Unit,
    updateStatus: String,
    availableUpdate: ReleaseUpdate?,
    onCheckForUpdates: () -> Unit,
    onDismissUpdate: () -> Unit,
    onOpenUpdate: (ReleaseUpdate) -> Unit,
) {
    val settings by store.settings.collectAsState()
    val status by ServiceStatus.value.collectAsState()
    var fps by remember(settings.fps) { mutableFloatStateOf(settings.fps.toFloat()) }
    var quality by remember(settings.jpegQuality) { mutableFloatStateOf(settings.jpegQuality.toFloat()) }

    if (availableUpdate != null) {
        AlertDialog(
            onDismissRequest = onDismissUpdate,
            title = { Text("Atualização do PocketCam") },
            text = {
                Text(
                    "Uma nova versão está disponível.\n\n" +
                        "Instalada: ${BuildConfig.VERSION_NAME.substringBefore('-')}\n" +
                        "Disponível: ${availableUpdate.version}\n\n" +
                        "Deseja abrir o download do APK agora?",
                )
            },
            confirmButton = { Button(onClick = { onOpenUpdate(availableUpdate) }) { Text("Abrir download") } },
            dismissButton = { Button(onClick = onDismissUpdate) { Text("Agora não") } },
        )
    }

    Column(
        modifier = Modifier.fillMaxSize().background(Color(0xFF071A1D)).verticalScroll(rememberScrollState()).padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Text("POCKETCAM", color = Color(0xFF00D4A6), fontSize = 13.sp, fontWeight = FontWeight.Bold)
        Text("Seu Android, agora uma webcam.", fontSize = 27.sp, fontWeight = FontWeight.Bold)

        Card(colors = CardDefaults.cardColors(containerColor = Color(0xFF102B2F)), shape = RoundedCornerShape(18.dp)) {
            Column(Modifier.fillMaxWidth().padding(18.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(if (status.running) "Transmitindo" else "Pronto para conectar", fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
                Text(
                    "Wi-Fi/USB: ${status.wifiClients}  ·  Bluetooth: ${status.bluetoothClients}",
                    color = Color(0xFFA8C7C8),
                )
                status.lastError?.let { Text(it, color = Color(0xFFFF938A), fontSize = 12.sp) }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    Button(
                        onClick = if (!hasCameraPermission) onRequestPermission else onStart,
                        colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF00D4A6), contentColor = Color(0xFF041416)),
                    ) { Text(if (status.running) "Reiniciar" else "Iniciar") }
                    if (status.running) Button(onClick = onStop) { Text("Parar") }
                }
            }
        }

        Text("Resolução", fontWeight = FontWeight.SemiBold)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            listOf(640 to 480, 1280 to 720, 1920 to 1080).forEach { resolution ->
                Button(
                    onClick = { store.update(settings.copy(width = resolution.first, height = resolution.second)) },
                    colors = ButtonDefaults.buttonColors(
                        containerColor = if (settings.width == resolution.first) Color(0xFF00D4A6) else Color(0xFF17383C),
                        contentColor = if (settings.width == resolution.first) Color(0xFF041416) else Color.White,
                    ),
                ) { Text(if (resolution.first == 1920) "1080p" else if (resolution.first == 1280) "720p" else "480p") }
            }
        }

        Text("FPS: ${fps.toInt()}", fontWeight = FontWeight.SemiBold)
        Slider(
            value = fps,
            onValueChange = { fps = it },
            onValueChangeFinished = { store.update(settings.copy(fps = fps.toInt())) },
            valueRange = 5f..30f,
            steps = 24,
        )
        Text("Qualidade JPEG: ${quality.toInt()}%", fontWeight = FontWeight.SemiBold)
        Slider(
            value = quality,
            onValueChange = { quality = it },
            onValueChangeFinished = { store.update(settings.copy(jpegQuality = quality.toInt())) },
            valueRange = 40f..95f,
            steps = 54,
        )
        Button(onClick = { store.update(settings.copy(lens = if (settings.lens == "back") "front" else "back")) }) {
            Text(if (settings.lens == "back") "Usando câmera traseira" else "Usando câmera frontal")
        }
        Text("Atualizações", fontWeight = FontWeight.SemiBold)
        Text(updateStatus, color = Color(0xFFA8C7C8), fontSize = 13.sp)
        Button(onClick = onCheckForUpdates) { Text("Verificar atualizações") }
        Spacer(Modifier.height(4.dp))
        Text(
            "Mantenha o app aberto. USB é preferido; Wi-Fi e Bluetooth assumem automaticamente.",
            color = Color(0xFFA8C7C8),
            fontSize = 13.sp,
        )
    }
}

private val pocketCamColors = darkColorScheme(
    primary = Color(0xFF00D4A6),
    background = Color(0xFF071A1D),
    surface = Color(0xFF102B2F),
    onPrimary = Color(0xFF041416),
    onBackground = Color.White,
    onSurface = Color.White,
)
