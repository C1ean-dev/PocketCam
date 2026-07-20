package com.pocketcam.android.camera

import android.graphics.ImageFormat
import android.graphics.Rect
import android.graphics.YuvImage
import androidx.camera.core.ImageProxy
import java.io.ByteArrayOutputStream

object YuvToJpeg {
    fun encode(image: ImageProxy, quality: Int): ByteArray {
        val nv21 = toNv21(image)
        return ByteArrayOutputStream().use { output ->
            val success = YuvImage(nv21, ImageFormat.NV21, image.width, image.height, null)
                .compressToJpeg(Rect(0, 0, image.width, image.height), quality, output)
            check(success) { "Android JPEG encoder rejected the camera frame" }
            output.toByteArray()
        }
    }

    private fun toNv21(image: ImageProxy): ByteArray {
        val width = image.width
        val height = image.height
        val result = ByteArray(width * height * 3 / 2)
        copyPlane(image.planes[0], width, height, result, 0, 1)
        // NV21 interleaves V then U.
        copyPlane(image.planes[2], width / 2, height / 2, result, width * height, 2)
        copyPlane(image.planes[1], width / 2, height / 2, result, width * height + 1, 2)
        return result
    }

    private fun copyPlane(
        plane: ImageProxy.PlaneProxy,
        planeWidth: Int,
        planeHeight: Int,
        output: ByteArray,
        outputOffset: Int,
        outputPixelStride: Int,
    ) {
        val buffer = plane.buffer.duplicate()
        val rowStride = plane.rowStride
        val pixelStride = plane.pixelStride
        var target = outputOffset
        for (row in 0 until planeHeight) {
            val rowStart = row * rowStride
            for (column in 0 until planeWidth) {
                output[target] = buffer.get(rowStart + column * pixelStride)
                target += outputPixelStride
            }
        }
    }
}

