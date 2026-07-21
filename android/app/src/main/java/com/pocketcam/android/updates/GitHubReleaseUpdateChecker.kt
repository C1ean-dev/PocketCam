package com.pocketcam.android.updates

import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject

data class ReleaseUpdate(
    val version: AppVersion,
    val tagName: String,
    val releasePageUri: URI,
    val androidDownloadUri: URI?,
)

class GitHubReleaseUpdateChecker {
    suspend fun findUpdate(installedVersionName: String): ReleaseUpdate? = withContext(Dispatchers.IO) {
        val installedVersion = AppVersion.parse(installedVersionName) ?: return@withContext null
        val connection = (URL(LATEST_RELEASE_ENDPOINT).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 10_000
            readTimeout = 10_000
            setRequestProperty("Accept", "application/vnd.github+json")
            setRequestProperty("User-Agent", "PocketCam-Android/$installedVersion")
            setRequestProperty("X-GitHub-Api-Version", "2026-03-10")
        }

        try {
            if (connection.responseCode == HttpURLConnection.HTTP_NOT_FOUND) return@withContext null
            if (connection.responseCode !in 200..299) {
                throw IllegalStateException("GitHub respondeu com HTTP ${connection.responseCode}.")
            }

            parseRelease(connection.inputStream.bufferedReader().use { it.readText() }, installedVersion)
        } finally {
            connection.disconnect()
        }
    }

    internal fun parseRelease(json: String, installedVersion: AppVersion): ReleaseUpdate? {
        val release = JSONObject(json)
        if (release.optBoolean("draft") || release.optBoolean("prerelease")) return null

        val tagName = release.optString("tag_name")
        val availableVersion = AppVersion.parse(tagName) ?: return null
        if (availableVersion <= installedVersion) return null
        val releasePageUri = release.optString("html_url").asHttpsUri() ?: return null

        val assets = release.optJSONArray("assets")
        var androidDownloadUri: URI? = null
        if (assets != null) {
            for (index in 0 until assets.length()) {
                val asset = assets.optJSONObject(index) ?: continue
                if (asset.optString("name").equals(ANDROID_ASSET_NAME, ignoreCase = true)) {
                    androidDownloadUri = asset.optString("browser_download_url").asHttpsUri()
                    break
                }
            }
        }

        return ReleaseUpdate(availableVersion, tagName, releasePageUri, androidDownloadUri)
    }

    private fun String.asHttpsUri(): URI? = runCatching { URI(this) }
        .getOrNull()
        ?.takeIf { it.isAbsolute && it.scheme.equals("https", ignoreCase = true) }

    private companion object {
        const val LATEST_RELEASE_ENDPOINT = "https://api.github.com/repos/C1ean-dev/PocketCam/releases/latest"
        const val ANDROID_ASSET_NAME = "PocketCam-Android.apk"
    }
}
