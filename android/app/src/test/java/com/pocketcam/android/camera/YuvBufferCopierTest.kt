package com.pocketcam.android.camera

import java.nio.ByteBuffer
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class YuvBufferCopierTest {
    @Test
    fun copiesRowsWithoutPadding() {
        val output = ByteArray(8)

        val copied = YuvBufferCopier.tryCopyContiguousRows(
            ByteBuffer.wrap(byteArrayOf(1, 2, 3, 4, 5, 6, 7, 8)),
            rowStride = 4,
            rowBytes = 4,
            rowCount = 2,
            destination = output,
            destinationOffset = 0,
        )

        assertTrue(copied)
        assertArrayEquals(byteArrayOf(1, 2, 3, 4, 5, 6, 7, 8), output)
    }

    @Test
    fun skipsRowPaddingAndRespectsBufferPosition() {
        val source = ByteBuffer.wrap(byteArrayOf(99, 1, 2, 3, 0, 0, 4, 5, 6, 0, 0)).apply { position(1) }
        val output = ByteArray(6)

        val copied = YuvBufferCopier.tryCopyContiguousRows(source, 5, 3, 2, output, 0)

        assertTrue(copied)
        assertArrayEquals(byteArrayOf(1, 2, 3, 4, 5, 6), output)
    }

    @Test
    fun rejectsTruncatedPlane() {
        assertFalse(
            YuvBufferCopier.tryCopyContiguousRows(
                ByteBuffer.wrap(byteArrayOf(1, 2, 3)),
                rowStride = 4,
                rowBytes = 4,
                rowCount = 1,
                destination = ByteArray(4),
                destinationOffset = 0,
            ),
        )
    }
}
