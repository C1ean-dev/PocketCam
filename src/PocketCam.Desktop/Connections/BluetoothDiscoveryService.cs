using System.Threading.Channels;
using InTheHand.Net.Sockets;
using PocketCam.Core.Connections;

namespace PocketCam.Desktop.Connections;

public sealed class BluetoothDiscoveryService : IEndpointDiscovery
{
    public async Task DiscoverAsync(ChannelWriter<TransportEndpoint> endpoints, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = new BluetoothClient();
                foreach (var device in client.PairedDevices)
                {
                    await endpoints.WriteAsync(
                        new TransportEndpoint(
                            $"bluetooth:{device.DeviceAddress}",
                            TransportKind.Bluetooth,
                            device.DeviceAddress.ToString(),
                            0,
                            device.DeviceName),
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (PlatformNotSupportedException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
        }
    }
}

