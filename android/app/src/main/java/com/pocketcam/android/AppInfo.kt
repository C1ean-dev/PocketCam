package com.pocketcam.android

import android.content.Context

@Suppress("DEPRECATION")
fun Context.pocketCamVersionName(): String =
    packageManager.getPackageInfo(packageName, 0).versionName ?: "0.0.0"
