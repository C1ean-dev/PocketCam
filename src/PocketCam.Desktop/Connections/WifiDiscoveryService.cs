using System.Net;
using System.Net.Sockets;
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
        client.JoinMulticastGroup(IPAddress.Parse(ProtocolConstants.DiscoveryMulticastAddress));

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
                // Ignore unrelated UDP traffic on the discovery port.
            }
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

