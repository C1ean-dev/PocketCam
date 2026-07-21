package com.pocketcam.android.updates

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class GitHubReleaseUpdateCheckerTest {
    private val checker = GitHubReleaseUpdateChecker()

    @Test
    fun returnsNewerStableReleaseAndAndroidApk() {
        val update = checker.parseRelease(
            releaseJson(tag = "v0.2.0", assetName = "PocketCam-Android.apk"),
            installedVersion = AppVersion(0, 1, 2),
        )

        requireNotNull(update)
        assertEquals(AppVersion(0, 2, 0), update.version)
        assertEquals("v0.2.0", update.tagName)
        assertEquals(
            "https://github.com/C1ean-dev/PocketCam/releases/download/v0.2.0/PocketCam-Android.apk",
            update.androidDownloadUri.toString(),
        )
    }

    @Test
    fun matchesAndroidAssetNameIgnoringCase() {
        val update = checker.parseRelease(
            releaseJson(tag = "v1.0.0", assetName = "pocketcam-android.APK"),
            installedVersion = AppVersion(0, 9, 0),
        )

        assertTrue(update?.androidDownloadUri?.path?.endsWith("pocketcam-android.APK") == true)
    }

    @Test
    fun ignoresSameOlderDraftAndPrereleaseVersions() {
        val installed = AppVersion(0, 2, 0)

        assertNull(checker.parseRelease(releaseJson(tag = "v0.2.0"), installed))
        assertNull(checker.parseRelease(releaseJson(tag = "v0.1.9"), installed))
        assertNull(checker.parseRelease(releaseJson(tag = "v0.3.0", draft = true), installed))
        assertNull(checker.parseRelease(releaseJson(tag = "v0.3.0", prerelease = true), installed))
    }

    @Test
    fun fallsBackToReleasePageWhenApkAssetIsMissing() {
        val update = checker.parseRelease(
            releaseJson(tag = "v0.3.0", assetName = "PocketCam-Windows-win-x64.zip"),
            installedVersion = AppVersion(0, 2, 0),
        )

        requireNotNull(update)
        assertNull(update.androidDownloadUri)
        assertEquals("https://github.com/C1ean-dev/PocketCam/releases/tag/v0.3.0", update.releasePageUri.toString())
    }

    private fun releaseJson(
        tag: String,
        assetName: String? = null,
        draft: Boolean = false,
        prerelease: Boolean = false,
    ): String {
        val assets = if (assetName == null) {
            "[]"
        } else {
            """[{"name":"$assetName","browser_download_url":"https://github.com/C1ean-dev/PocketCam/releases/download/$tag/$assetName"}]"""
        }
        return """
            {
              "tag_name": "$tag",
              "html_url": "https://github.com/C1ean-dev/PocketCam/releases/tag/$tag",
              "draft": $draft,
              "prerelease": $prerelease,
              "assets": $assets
            }
        """.trimIndent()
    }
}
