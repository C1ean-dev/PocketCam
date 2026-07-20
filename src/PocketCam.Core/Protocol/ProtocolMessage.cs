namespace PocketCam.Core.Protocol;

public sealed record ProtocolMessage(
    MessageType Type,
    ushort Flags,
    uint Sequence,
    long TimestampMicroseconds,
    byte[] Payload)
{
    public static ProtocolMessage Create(MessageType type, uint sequence, byte[]? payload = null, ushort flags = 0)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = (now.ToUnixTimeMilliseconds() * 1_000) + (now.Ticks % TimeSpan.TicksPerMillisecond) / 10;
        return new ProtocolMessage(type, flags, sequence, timestamp, payload ?? []);
    }
}

