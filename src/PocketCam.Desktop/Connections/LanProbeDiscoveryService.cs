using System.Threading.Channels;
using System.Net.Sockets;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Connections;

public sealed class LanProbeDiscoveryService : IEndpointDiscovery
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(900);

    public async Task DiscoverAsync(ChannelWriter<TransportEndpoint> endpoints, CancellationToken cancellationToken)
    {
        // Give multicast/broadcast discovery a chance before using the bounded TCP fallback.
        await Task.Delay(InitialDelay, cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            var candidates = LocalNetwork.GetIpv4Interfaces()
                .SelectMany(item => LocalNetwork.GetProbeCandidates(item.Address, item.PrefixLength))
                .Distinct()
                .ToArray();

            await Parallel.ForEachAsync(
                candidates,
                new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 64 },
                async (address, token) =>
                {
                    var endpoint = await ProbeAsync(address, ProtocolConstants.TcpPort, ProbeTimeout, token).ConfigureAwait(false);
                    if (endpoint is not null) await endpoints.WriteAsync(endpoint, token).ConfigureAwait(false);
                }).ConfigureAwait(false);

            await Task.Delay(ScanInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<TransportEndpoint?> ProbeAsync(
        IPAddress address,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        using var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
        try
        {
            await client.ConnectAsync(address, port, timeoutCancellation.Token).ConfigureAwait(false);
            var message = await ProtocolCodec.ReadAsync(client.GetStream(), timeoutCancellation.Token).ConfigureAwait(false);
            if (message.Type != MessageType.Hello) return null;
            var hello = JsonPayload.Deserialize<HelloMessage>(message.Payload);
            if (string.IsNullOrWhiteSpace(hello.DeviceId) || string.IsNullOrWhiteSpace(hello.DeviceName)) return null;

            return new TransportEndpoint(
                $"wifi:{hello.DeviceId}:{address}",
                TransportKind.WiFi,
                address.ToString(),
                port,
                hello.DeviceName,
                hello.DeviceId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
