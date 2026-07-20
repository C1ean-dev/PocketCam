using System.Net.Sockets;
using InTheHand.Net;
using InTheHand.Net.Sockets;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Connections;

public static class TransportConnector
{
    public static async Task<Stream> ConnectAsync(TransportEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (endpoint.Kind == TransportKind.Bluetooth)
        {
            return await ConnectBluetoothAsync(endpoint, cancellationToken).ConfigureAwait(false);
        }

        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken).ConfigureAwait(false);
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            return new OwnedStream(client.GetStream(), client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<Stream> ConnectBluetoothAsync(TransportEndpoint endpoint, CancellationToken cancellationToken)
    {
        var client = new BluetoothClient
        {
            Authenticate = true,
            Encrypt = true,
        };
        try
        {
            await Task.Run(
                () => client.Connect(BluetoothAddress.Parse(endpoint.Address), ProtocolConstants.BluetoothServiceId),
                cancellationToken).ConfigureAwait(false);
            return new OwnedStream(client.GetStream(), client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
