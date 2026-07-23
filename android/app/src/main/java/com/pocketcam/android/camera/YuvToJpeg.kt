package com.pocketcam.android.camera

import android.graphics.ImageFormat
import android.graphics.Rect
import android.graphics.YuvImage
import androidx.camera.core.ImageProxy
import java.nio.ByteBuffer

internal class YuvToJpegEncoder {
    private var nv21 = ByteArray(0)
    private var output = DetachableByteArrayOutputStream()

    @Synchronized
    fun encode(image: ImageProxy, quality: Int): EncodedJpeg {
        val requiredBytes = image.width * image.height * 3 / 2
        if (nv21.size != requiredBytes) {
            nv21 = ByteArray(requiredBytes)
            output = DetachableByteArrayOutputStream(image.width * image.height / 4)
        } else {
            output.reset()
        }

        copyYuv420ToNv21(image, nv21)
        return encode(nv21, image.width, image.height, quality)
    }

    @Synchronized
    fun encode(nv21: ByteArray, width: Int, height: Int, quality: Int): EncodedJpeg {
        val requiredBytes = width * height * 3 / 2
        require(nv21.size >= requiredBytes)
        if (output.size() > 0) output.reset()
        val success = YuvImage(nv21, ImageFormat.NV21, width, height, null)
            .compressToJpeg(Rect(0, 0, width, height), quality, output)
        check(success) { "Android JPEG encoder rejected the camera frame" }
        return output.detach()
    }
}

/**
 * Copies CameraX's native YUV_420_888 planes into the NV21 layout expected by
 * [android.graphics.YuvImage], without asking CameraX to perform a second
 * format conversion first.
 */
internal fun copyYuv420ToNv21(image: ImageProxy, destination: ByteArray) {
    val width = image.width
    val height = image.height
    val planes = image.planes
    check(planes.size >= 3) { "NV21 camera frame did not expose three planes" }
    require(destination.size >= width * height * 3 / 2)

    YuvBufferCopier.copyContiguousRows(
        planes[0].buffer,
        planes[0].rowStride,
        width,
        height,
        destination,
        0,
    )

    // YUV_420_888 exposes U then V planes. NV21 stores the same samples as VU.
    // The common camera layout has pixelStride=2 and already interleaves the
    // chroma; the fallback handles devices that expose separate pixel samples.
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
        require(outputRowBytes > 0 && outputRowBytes % 2 == 0)
        require(rowCount > 0)
        val chromaWidth = outputRowBytes / 2
        require(destinationOffset >= 0 && destinationOffset + outputRowBytes * rowCount <= destination.size)
        val v = vSource.duplicate()
        val u = uSource.duplicate()
        val vBase = v.position()
        val uBase = u.position()
        val vLast = vBase + (rowCount - 1) * vRowStride + (chromaWidth - 1) * vPixelStride
        val uLast = uBase + (rowCount - 1) * uRowStride + (chromaWidth - 1) * uPixelStride
        require(vPixelStride > 0 && uPixelStride > 0 && vLast in vBase until v.limit() && uLast in uBase until u.limit())
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
