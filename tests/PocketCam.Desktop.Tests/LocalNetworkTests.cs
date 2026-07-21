using System.Net;
using System.Net.Sockets;
using PocketCam.Core.Protocol;
using PocketCam.Desktop.Connections;

namespace PocketCam.Desktop.Tests;

public sealed class LocalNetworkTests
{
    [Fact]
    public void CalculatesBroadcastAndBoundsProbeRangeToSlash24()
    {
        var local = IPAddress.Parse("192.168.18.15");

        Assert.Equal(IPAddress.Parse("192.168.18.255"), LocalNetwork.GetBroadcastAddress(local, 24));
        var candidates = LocalNetwork.GetProbeCandidates(local, 16);
        Assert.Equal(253, candidates.Count);
        Assert.DoesNotContain(local, candidates);
        Assert.Contains(IPAddress.Parse("192.168.18.1"), candidates);
        Assert.Contains(IPAddress.Parse("192.168.18.254"), candidates);
    }

    [Fact]
    public async Task ProbeAcceptsOnlyAValidPocketCamHello()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            var hello = new HelloMessage("phone-1", "Android encontrado", "0.1.2", ["jpeg"]);
            await ProtocolCodec.WriteAsync(
                stream,
                ProtocolMessage.Create(MessageType.Hello, 1, JsonPayload.Serialize(hello)),
                timeout.Token);
        }, timeout.Token);

        var endpoint = await LanProbeDiscoveryService.ProbeAsync(
            IPAddress.Loopback,
            port,
            TimeSpan.FromSeconds(2),
            timeout.Token);
        await server;

        Assert.NotNull(endpoint);
        Assert.Equal("phone-1", endpoint.ExpectedDeviceId);
        Assert.Equal("Android encontrado", endpoint.DeviceName);
    }

    [Fact]
    public async Task ProbeRejectsAnUnrelatedTcpService()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await client.GetStream().WriteAsync("HTTP/1.1 200 OK\r\n\r\n"u8.ToArray(), timeout.Token);
        }, timeout.Token);

        var endpoint = await LanProbeDiscoveryService.ProbeAsync(
            IPAddress.Loopback,
            port,
            TimeSpan.FromSeconds(2),
            timeout.Token);
        await server;

        Assert.Null(endpoint);
    }
}
