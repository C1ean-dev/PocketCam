package com.pocketcam.android.protocol

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream

class WireProtocolTest {
    @Test
    fun messageRoundTrip() {
        val original = WireProtocol.Message(
            type = WireProtocol.Type.STATUS,
            flags = 3,
            sequence = 42,
            timestampMicros = 123456789,
            payload = byteArrayOf(1, 2, 3),
        )
        val bytes = ByteArrayOutputStream().also { WireProtocol.write(it, original) }.toByteArray()

        val result = WireProtocol.read(ByteArrayInputStream(bytes))

        assertEquals(original.type, result.type)
        assertEquals(original.flags, result.flags)
        assertEquals(original.sequence, result.sequence)
        assertEquals(original.timestampMicros, result.timestampMicros)
        assertArrayEquals(original.payload, result.payload)
    }

    @Test
    fun encodingMatchesDotNetGoldenVector() {
        val message = WireProtocol.Message(
            type = WireProtocol.Type.STATUS,
            flags = 3,
            sequence = 42,
            timestampMicros = 123456789,
            payload = byteArrayOf(1, 2, 3),
        )
        val encoded = ByteArrayOutputStream().also { WireProtocol.write(it, message) }.toByteArray()

        assertArrayEquals(
            byteArrayOf(
                0x50, 0x43, 0x4d, 0x31, 0x01, 0x06, 0x03, 0x00,
                0x03, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00,
                0x15, 0xcd.toByte(), 0x5b, 0x07, 0x00, 0x00, 0x00, 0x00,
                0x1d, 0x80.toByte(), 0xbc.toByte(), 0x55, 0x01, 0x02, 0x03,
            ),
            encoded,
        )
    }

    @Test(expected = ProtocolException::class)
    fun rejectsCorruptPayload() {
        val bytes = ByteArrayOutputStream().also {
            WireProtocol.write(it, WireProtocol.Message(WireProtocol.Type.STATUS, sequence = 1, payload = byteArrayOf(1)))
        }.toByteArray()
        bytes[bytes.lastIndex] = 2
        WireProtocol.read(ByteArrayInputStream(bytes))
    }
}
