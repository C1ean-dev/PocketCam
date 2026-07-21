package com.pocketcam.android.camera

import android.graphics.ImageFormat
import android.graphics.Rect
import android.graphics.YuvImage
import androidx.camera.core.ImageProxy
import java.io.ByteArrayOutputStream
import java.nio.ByteBuffer

class YuvToJpegEncoder {
    private var nv21 = ByteArray(0)
    private var output = ByteArrayOutputStream()

    @Synchronized
    fun encode(image: ImageProxy, quality: Int): ByteArray {
        val requiredBytes = image.width * image.height * 3 / 2
        if (nv21.size != requiredBytes) {
            nv21 = ByteArray(requiredBytes)
            output = ByteArrayOutputStream((image.width * image.height / 4).coerceAtLeast(16 * 1024))
        } else {
            output.reset()
        }

        copyNv21(image, nv21)
        val success = YuvImage(nv21, ImageFormat.NV21, image.width, image.height, null)
            .compressToJpeg(Rect(0, 0, image.width, image.height), quality, output)
        check(success) { "Android JPEG encoder rejected the camera frame" }
        return output.toByteArray()
    }

    private fun copyNv21(image: ImageProxy, destination: ByteArray) {
        val width = image.width
        val height = image.height
        val planes = image.planes
        check(planes.size >= 3) { "NV21 camera frame did not expose three planes" }

        YuvBufferCopier.copyContiguousRows(
            planes[0].buffer,
            planes[0].rowStride,
            width,
            height,
            destination,
            0,
        )

        // CameraX OUTPUT_IMAGE_FORMAT_NV21 guarantees that plane 2 starts at V and
        // exposes the VU-interleaved chroma bytes, so no per-pixel YUV conversion is needed.
        val chromaCopied = YuvBufferCopier.tryCopyContiguousRows(
            planes[2].buffer,
            planes[2].rowStride,
            width,
            height / 2,
            destination,
            width * height,
        )
        if (!chromaCopied) {
            YuvBufferCopier.copyInterleavedChroma(
                planes[2].buffer,
                planes[2].rowStride,
                planes[2].pixelStride,
                planes[1].buffer,
                planes[1].rowStride,
                planes[1].pixelStride,
                width,
                height / 2,
                destination,
                width * height,
            )
        }
    }
}

internal object YuvBufferCopier {
    fun copyContiguousRows(
        source: ByteBuffer,
        rowStride: Int,
        rowBytes: Int,
        rowCount: Int,
        destination: ByteArray,
        destinationOffset: Int,
    ) {
        check(tryCopyContiguousRows(source, rowStride, rowBytes, rowCount, destination, destinationOffset)) {
            "Camera plane is smaller than its declared dimensions"
        }
    }

    fun tryCopyContiguousRows(
        source: ByteBuffer,
        rowStride: Int,
        rowBytes: Int,
        rowCount: Int,
        destination: ByteArray,
        destinationOffset: Int,
    ): Boolean {
        if (rowBytes <= 0 || rowCount <= 0 || rowStride < rowBytes) return false
        val buffer = source.duplicate()
        val base = buffer.position()
        val lastByteExclusive = base + ((rowCount - 1) * rowStride) + rowBytes
        if (lastByteExclusive > buffer.limit() || destinationOffset + rowBytes * rowCount > destination.size) return false

        var target = destinationOffset
        for (row in 0 until rowCount) {
            buffer.position(base + row * rowStride)
            buffer.get(destination, target, rowBytes)
            target += rowBytes
        }
        return true
    }

    fun copyInterleavedChroma(
        vSource: ByteBuffer,
        vRowStride: Int,
        vPixelStride: Int,
        uSource: ByteBuffer,
        uRowStride: Int,
        uPixelStride: Int,
        outputRowBytes: Int,
        rowCount: Int,
        destination: ByteArray,
        destinationOffset: Int,
    ) {
        val v = vSource.duplicate()
        val u = uSource.duplicate()
        val vBase = v.position()
        val uBase = u.position()
        val chromaWidth = outputRowBytes / 2
        var target = destinationOffset
        for (row in 0 until rowCount) {
            val vRow = vBase + row * vRowStride
            val uRow = uBase + row * uRowStride
            for (column in 0 until chromaWidth) {
                destination[target++] = v.get(vRow + column * vPixelStride)
                destination[target++] = u.get(uRow + column * uPixelStride)
            }
        }
    }
}
