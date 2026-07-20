using PocketCam.Core.Protocol;

namespace PocketCam.Core.Tests;

public sealed class ProtocolCodecTests
{
    [Fact]
    public async Task RoundTripPreservesEveryField()
    {
        var original = new ProtocolMessage(MessageType.Status, 7, 42, 123_456_789, [1, 2, 3, 4]);
        await using var stream = new MemoryStream();

        await ProtocolCodec.WriteAsync(stream, original);
        stream.Position = 0;
        var decoded = await ProtocolCodec.ReadAsync(stream);

        Assert.Equal(original.Type, decoded.Type);
        Assert.Equal(original.Flags, decoded.Flags);
        Assert.Equal(original.Sequence, decoded.Sequence);
        Assert.Equal(original.TimestampMicroseconds, decoded.TimestampMicroseconds);
        Assert.Equal(original.Payload, decoded.Payload);
    }

    [Fact]
    public async Task EncodingMatchesAndroidGoldenVector()
    {
        var message = new ProtocolMessage(
            MessageType.Status,
            3,
            42,
            123_456_789,
            [1, 2, 3]);
        await using var stream = new MemoryStream();

        await ProtocolCodec.WriteAsync(stream, message);

        Assert.Equal(
            Convert.FromHexString("50434D3101060300030000002A00000015CD5B07000000001D80BC55010203"),
            stream.ToArray());
    }

    [Fact]
    public async Task CorruptedPayloadIsRejected()
    {
        await using var stream = new MemoryStream();
        await ProtocolCodec.WriteAsync(stream, ProtocolMessage.Create(MessageType.Status, 1, [1, 2, 3]));
        var bytes = stream.ToArray();
        bytes[^1] ^= 0xff;
        await using var corrupted = new MemoryStream(bytes);

        await Assert.ThrowsAsync<ProtocolException>(async () => await ProtocolCodec.ReadAsync(corrupted));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void FramePayloadRoundTrips(int rotation)
    {
        var original = new VideoFrame(1280, 720, (ushort)rotation, VideoCodec.Jpeg, [0xff, 0xd8, 0xff, 0xd9]);
        var decoded = VideoFrame.FromPayload(original.ToPayload());

        Assert.Equal(original.Width, decoded.Width);
        Assert.Equal(original.Height, decoded.Height);
        Assert.Equal(original.Rotation, decoded.Rotation);
        Assert.Equal(original.Codec, decoded.Codec);
        Assert.Equal(original.Data, decoded.Data);
    }

    [Fact]
    public void StandardCrcVectorMatches()
    {
        Assert.Equal(0xcbf43926u, Crc32.Compute("123456789"u8));
    }
}
