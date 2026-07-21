using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Connections;

public sealed class WifiDiscoveryService : IEndpointDiscovery
{
    public async Task DiscoverAsync(ChannelWriter<TransportEndpoint> endpoints, CancellationToken cancellationToken)
    {
        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, ProtocolConstants.DiscoveryPort));
        client.EnableBroadcast = true;
        var multicastAddress = IPAddress.Parse(ProtocolConstants.DiscoveryMulticastAddress);
        foreach (var (address, _) in LocalNetwork.GetIpv4Interfaces())
        {
            try { client.JoinMulticastGroup(multicastAddress, address); } catch (SocketException) { }
        }

        var probeTask = SendProbesAsync(client, multicastAddress, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var beacon = JsonSerializer.Deserialize<DiscoveryBeacon>(result.Buffer, JsonOptions);
                    if (beacon is null || beacon.Magic != "PCM1" || beacon.Version != 1 || string.IsNullOrWhiteSpace(beacon.DeviceId))
                    {
                        continue;
                    }

                    var address = result.RemoteEndPoint.Address.ToString();
                    await endpoints.WriteAsync(
                        new TransportEndpoint(
                            $"wifi:{beacon.DeviceId}:{address}",
                            TransportKind.WiFi,
                            address,
                            beacon.Port,
                            beacon.DeviceName,
                            beacon.DeviceId),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException)
                {
                    // Ignore probes and unrelated UDP traffic on the discovery port.
                }
            }
        }
        finally
        {
            try { await probeTask.ConfigureAwait(false); } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        }
    }

    private static async Task SendProbesAsync(UdpClient client, IPAddress multicastAddress, CancellationToken cancellationToken)
    {
        var payload = Encoding.ASCII.GetBytes(ProtocolConstants.DiscoveryProbe);
        while (!cancellationToken.IsCancellationRequested)
        {
            var targets = LocalNetwork.GetIpv4Interfaces()
                .Select(item => LocalNetwork.GetBroadcastAddress(item.Address, item.PrefixLength))
                .Append(IPAddress.Broadcast)
                .Append(multicastAddress)
                .Distinct();
            foreach (var target in targets)
            {
                try
                {
                    await client.SendAsync(
                        payload,
                        new IPEndPoint(target, ProtocolConstants.DiscoveryPort),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (SocketException)
                {
                    // Continue with the other interfaces and passive discovery.
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record DiscoveryBeacon(
        string Magic,
        int Version,
        string DeviceId,
        string DeviceName,
        int Port);
}
