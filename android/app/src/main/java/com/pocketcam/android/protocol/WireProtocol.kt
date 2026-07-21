package com.pocketcam.android.protocol

import java.io.EOFException
import java.io.InputStream
import java.io.OutputStream
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.zip.CRC32

object WireProtocol {
    const val VERSION: Byte = 1
    const val HEADER_SIZE = 28
    const val MAX_PAYLOAD_SIZE = 16 * 1024 * 1024
    const val TCP_PORT = 17890
    const val DISCOVERY_PORT = 17891
    const val DISCOVERY_GROUP = "239.255.88.88"
    const val DISCOVERY_PROBE = "PCM1_DISCOVER"
    const val BLUETOOTH_UUID = "7d5a6bf8-3c31-4f30-9fb8-84b85b8c9d11"
    private val MAGIC = byteArrayOf('P'.code.toByte(), 'C'.code.toByte(), 'M'.code.toByte(), '1'.code.toByte())

    enum class Type(val value: Byte) {
        HELLO(1), FRAME(2), SETTINGS(3), PING(4), PONG(5), STATUS(6), ERROR(0xff.toByte());

        companion object {
            fun from(value: Byte): Type = entries.firstOrNull { it.value == value }
                ?: throw ProtocolException("Unknown message type ${value.toUByte()}")
        }
    }

    data class Message(
        val type: Type,
        val flags: Short = 0,
        val sequence: Int,
        val timestampMicros: Long = System.currentTimeMillis() * 1_000,
        val payload: ByteArray = byteArrayOf(),
    )

    fun write(output: OutputStream, message: Message) {
        require(message.payload.size <= MAX_PAYLOAD_SIZE) { "Payload too large" }
        val header = ByteBuffer.allocate(HEADER_SIZE).order(ByteOrder.LITTLE_ENDIAN)
        header.put(MAGIC)
        header.put(VERSION)
        header.put(message.type.value)
        header.putShort(message.flags)
        header.putInt(message.payload.size)
        header.putInt(message.sequence)
        header.putLong(message.timestampMicros)
        header.putInt(crc32(message.payload).toInt())
        output.write(header.array())
        output.write(message.payload)
        output.flush()
    }

    fun read(input: InputStream): Message {
        val headerBytes = input.readExactly(HEADER_SIZE)
        val header = ByteBuffer.wrap(headerBytes).order(ByteOrder.LITTLE_ENDIAN)
        val magic = ByteArray(4).also(header::get)
        if (!magic.contentEquals(MAGIC)) throw ProtocolException("Invalid PocketCam magic")
        val version = header.get()
        if (version != VERSION) throw ProtocolException("Unsupported protocol version $version")
        val type = Type.from(header.get())
        val flags = header.short
        val size = header.int
        if (size !in 0..MAX_PAYLOAD_SIZE) throw ProtocolException("Invalid payload size $size")
        val sequence = header.int
        val timestamp = header.long
        val expectedCrc = header.int.toUInt().toLong()
        val payload = input.readExactly(size)
        if (crc32(payload) != expectedCrc) throw ProtocolException("Payload CRC32 does not match")
        return Message(type, flags, sequence, timestamp, payload)
    }

    private fun InputStream.readExactly(size: Int): ByteArray {
        val result = ByteArray(size)
        var offset = 0
        while (offset < size) {
            val count = read(result, offset, size - offset)
            if (count < 0) throw EOFException("PocketCam message ended early")
            offset += count
        }
        return result
    }

    private fun crc32(bytes: ByteArray): Long = CRC32().run {
        update(bytes)
        value
    }
}

class ProtocolException(message: String) : java.io.IOException(message)
