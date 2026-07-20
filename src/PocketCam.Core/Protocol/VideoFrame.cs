using System.Buffers.Binary;

namespace PocketCam.Core.Protocol;

public enum VideoCodec : byte
{
    Jpeg = 1,
}

public sealed record VideoFrame(ushort Width, ushort Height, ushort Rotation, VideoCodec Codec, byte[] Data)
{
    public byte[] ToPayload()
    {
        Validate(Width, Height, Rotation, Codec, Data.Length);
        var payload = new byte[8 + Data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0), Width);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), Height);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), Rotation);
        payload[6] = (byte)Codec;
        Data.CopyTo(payload, 8);
        return payload;
    }

    public static VideoFrame FromPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
        {
            throw new ProtocolException("Frame payload is shorter than its metadata.");
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
        var rotation = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
        var codec = (VideoCodec)payload[6];
        Validate(width, height, rotation, codec, payload.Length - 8);
        return new VideoFrame(width, height, rotation, codec, payload[8..].ToArray());
    }

    private static void Validate(ushort width, ushort height, ushort rotation, VideoCodec codec, int dataLength)
    {
        if (width == 0 || height == 0 || width > 7680 || height > 4320)
        {
            throw new ProtocolException($"Invalid frame dimensions {width}x{height}.");
        }

        if (rotation is not (0 or 90 or 180 or 270))
        {
            throw new ProtocolException($"Invalid frame rotation {rotation}.");
        }

        if (codec != VideoCodec.Jpeg)
        {
            throw new ProtocolException($"Unsupported video codec {(byte)codec}.");
        }

        if (dataLength == 0)
        {
            throw new ProtocolException("Frame contains no encoded data.");
        }
    }
}

