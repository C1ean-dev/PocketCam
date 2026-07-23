using System.Net;
using System.Net.Sockets;
using System.Reflection;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;
using PocketCam.Desktop.Connections;

namespace PocketCam.Desktop.Tests;

public sealed class ConnectionManagerIntegrationTests
{
    [Fact]
    public async Task SettingsFromBackupRouteUpdateTheActiveDevice()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var usbListener = StartListener(out var usbPort);
        using var wifiListener = StartListener(out var wifiPort);
        await using var manager = new ConnectionManager();

        var usbPeerTask = AcceptAndIdentifyAsync(usbListener, timeout.Token);
        await ConnectAsync(
            manager,
            new TransportEndpoint("usb:phone-sync", TransportKind.Usb, "127.0.0.1", usbPort, "Android USB", "phone-sync"),
            timeout.Token);
        using var usbPeer = await usbPeerTask;

        var wifiPeerTask = AcceptAndIdentifyAsync(wifiListener, timeout.Token);
        await ConnectAsync(
            manager,
            new TransportEndpoint("wifi:phone-sync", TransportKind.WiFi, "127.0.0.1", wifiPort, "Android Wi-Fi", "phone-sync"),
            timeout.Token);
        using var wifiPeer = await wifiPeerTask;

        var expected = new CameraSettings(1920, 1080, 60, 75, "front");
        var received = new TaskCompletionSource<CameraSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.SettingsChanged += settings => received.TrySetResult(settings);

        await ProtocolCodec.WriteAsync(
            wifiPeer.GetStream(),
            ProtocolMessage.Create(MessageType.Settings, 2, JsonPayload.Serialize(expected)),
            timeout.Token);

        var actual = await received.Task.WaitAsync(timeout.Token);
        Assert.Equal(expected, actual);
    }

    private static async Task<TcpClient> AcceptAndIdentifyAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var hello = new HelloMessage("phone-sync", "Synced Android", "0.1.7", ["settings-sync"]);
        await ProtocolCodec.WriteAsync(
            client.GetStream(),
            ProtocolMessage.Create(MessageType.Hello, 1, JsonPayload.Serialize(hello)),
            cancellationToken);
        return client;
    }

    private static Task ConnectAsync(
        ConnectionManager manager,
        TransportEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var method = typeof(ConnectionManager).GetMethod(
            "ConnectEndpointAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(manager, [endpoint, cancellationToken]));
    }

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }
}
