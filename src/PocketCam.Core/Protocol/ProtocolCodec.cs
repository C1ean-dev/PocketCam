using System.Buffers.Binary;

namespace PocketCam.Core.Protocol;

public static class ProtocolCodec
{
    private static ReadOnlySpan<byte> Magic => "PCM1"u8;

    public static async ValueTask WriteAsync(Stream stream, ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        if (message.Payload.Length > ProtocolConstants.MaxPayloadSize)
        {
            throw new ProtocolException($"Payload exceeds {ProtocolConstants.MaxPayloadSize} bytes.");
        }

        var header = new byte[ProtocolConstants.HeaderSize];
        Magic.CopyTo(header);
        header[4] = ProtocolConstants.Version;
        header[5] = (byte)message.Type;
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), message.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)message.Payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), message.Sequence);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), message.TimestampMicroseconds);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), Crc32.Compute(message.Payload));

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (message.Payload.Length != 0)
        {
            await stream.WriteAsync(message.Payload, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<ProtocolMessage> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[ProtocolConstants.HeaderSize];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);

        if (!header.AsSpan(0, 4).SequenceEqual(Magic))
        {
            throw new ProtocolException("Invalid PocketCam magic.");
        }

        if (header[4] != ProtocolConstants.Version)
        {
            throw new ProtocolException($"Unsupported protocol version {header[4]}.");
        }

        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
        if (payloadLength > ProtocolConstants.MaxPayloadSize)
        {
            throw new ProtocolException($"Payload length {payloadLength} is outside the allowed range.");
        }

        var payload = new byte[(int)payloadLength];
        if (payload.Length != 0)
        {
            await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        }

        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24));
        if (Crc32.Compute(payload) != expectedCrc)
        {
            throw new ProtocolException("Payload CRC32 does not match.");
        }

        return new ProtocolMessage(
            (MessageType)header[5],
            BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)),
            BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16)),
            payload);
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> destination, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The PocketCam stream ended mid-message.");
            }

            offset += read;
        }
    }
}

