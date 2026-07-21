namespace PocketCam.Core.Protocol;

public static class ProtocolConstants
{
    public const int HeaderSize = 28;
    public const int MaxPayloadSize = 16 * 1024 * 1024;
    public const byte Version = 1;
    public const int TcpPort = 17_890;
    public const int DiscoveryPort = 17_891;
    public const string DiscoveryMulticastAddress = "239.255.88.88";
    public const string DiscoveryProbe = "PCM1_DISCOVER";
    public static readonly Guid BluetoothServiceId = new("7d5a6bf8-3c31-4f30-9fb8-84b85b8c9d11");
}
