using System.Threading.Channels;

namespace PocketCam.Desktop.Connections;

public interface IEndpointDiscovery
{
    Task DiscoverAsync(ChannelWriter<TransportEndpoint> endpoints, CancellationToken cancellationToken);
}

