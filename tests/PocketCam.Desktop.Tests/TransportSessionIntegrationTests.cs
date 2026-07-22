using System.Net;
using System.Net.Sockets;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;
using PocketCam.Desktop.Connections;

namespace PocketCam.Desktop.Tests;

public sealed class TransportSessionIntegrationTests
{
    [Fact]
    public async Task LoopbackPeerDeliversHelloAndVideoFrame()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = StartListener(out var port);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            var hello = new HelloMessage("phone-1", "Test Android", "0.1.0", ["jpeg", "settings"]);
            await ProtocolCodec.WriteAsync(
                stream,
                ProtocolMessage.Create(MessageType.Hello, 1, JsonPayload.Serialize(hello)),
                timeout.Token);
            var frame = new VideoFrame(640, 480, 90, VideoCodec.Jpeg, new byte[] { 0xff, 0xd8, 0xff, 0xd9 });
            await ProtocolCodec.WriteAsync(
                stream,
                ProtocolMessage.Create(MessageType.Frame, 2, frame.ToPayload()),
                timeout.Token);
            await Task.Delay(100, timeout.Token);
        }, timeout.Token);

        var endpoint = new TransportEndpoint("wifi:test", TransportKind.WiFi, "127.0.0.1", port, "Expected");
        await using var session = new TransportSession(endpoint);
        var received = new TaskCompletionSource<VideoFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.FrameReceived += (_, frame) => received.TrySetResult(frame);

        await session.StartAsync(timeout.Token);
        var frame = await received.Task.WaitAsync(timeout.Token);
        await server;

        Assert.Equal("phone-1", session.DeviceId);
        Assert.Equal("Test Android", session.DeviceName);
        Assert.Equal((ushort)640, frame.Width);
        Assert.Equal((ushort)480, frame.Height);
        Assert.Equal((ushort)90, frame.Rotation);
        Assert.Equal([0xff, 0xd8, 0xff, 0xd9], frame.Data.ToArray());
    }

    [Fact]
    public async Task SettingsAreSentToTheConnectedAndroidPeer()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = StartListener(out var port);
        var receivedSettings = new TaskCompletionSource<CameraSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            while (!timeout.IsCancellationRequested)
            {
                var message = await ProtocolCodec.ReadAsync(stream, timeout.Token);
                if (message.Type != MessageType.Settings) continue;
                receivedSettings.TrySetResult(JsonPayload.Deserialize<CameraSettings>(message.Payload));
                return;
            }
        }, timeout.Token);

        var endpoint = new TransportEndpoint("usb:test", TransportKind.Usb, "127.0.0.1", port, "Android USB");
        await using var session = new TransportSession(endpoint);
        await session.StartAsync(timeout.Token);

        await session.SendSettingsAsync(new CameraSettings(1920, 1080, 30, 90, "front"), timeout.Token);
        var settings = await receivedSettings.Task.WaitAsync(timeout.Token);
        await server;

        Assert.Equal(1920, settings.Width);
        Assert.Equal(1080, settings.Height);
        Assert.Equal(30, settings.Fps);
        Assert.Equal(90, settings.JpegQuality);
        Assert.Equal("front", settings.Lens);
    }

    [Fact]
    public async Task SettingsFromAndroidUpdateTheSessionState()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = StartListener(out var port);
        var expected = new CameraSettings(1280, 720, 30, 70, "back");
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            var hello = new HelloMessage("phone-sync", "Synced Android", "0.2.0", ["jpeg", "settings-sync"]);
            await ProtocolCodec.WriteAsync(
                stream,
                ProtocolMessage.Create(MessageType.Hello, 1, JsonPayload.Serialize(hello)),
                timeout.Token);
            await ProtocolCodec.WriteAsync(
                stream,
                ProtocolMessage.Create(MessageType.Settings, 2, JsonPayload.Serialize(expected)),
                timeout.Token);
            await Task.Delay(100, timeout.Token);
        }, timeout.Token);

        var endpoint = new TransportEndpoint("wifi:sync", TransportKind.WiFi, "127.0.0.1", port, "Expected");
        await using var session = new TransportSession(endpoint);
        var received = new TaskCompletionSource<CameraSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SettingsReceived += (_, settings) => received.TrySetResult(settings);

        await session.StartAsync(timeout.Token);
        var actual = await received.Task.WaitAsync(timeout.Token);
        await server;

        Assert.Equal(expected, actual);
        Assert.Equal(expected, session.CurrentSettings);
    }

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }
}
