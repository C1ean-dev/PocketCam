using System.Buffers.Binary;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PocketCam.Desktop.Connections;

public static class LocalNetwork
{
    public static IReadOnlyList<(IPAddress Address, int PrefixLength)> GetIpv4Interfaces()
    {
        var addresses = new List<(IPAddress Address, int PrefixLength)>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                addresses.AddRange(networkInterface.GetIPProperties().UnicastAddresses
                    .Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork && IsUsable(item.Address))
                    .Select(item => (item.Address, item.PrefixLength)));
            }
            catch (NetworkInformationException)
            {
                // An interface may disappear while its properties are being read.
            }
        }

        return addresses.Distinct().ToArray();
    }

    public static IReadOnlyList<IPAddress> GetProbeCandidates(IPAddress localAddress, int prefixLength)
    {
        if (localAddress.AddressFamily != AddressFamily.InterNetwork || !IsUsable(localAddress)) return [];

        // Bound active discovery to at most the local /24, even on large corporate/VPN networks.
        var effectivePrefix = Math.Clamp(Math.Max(prefixLength, 24), 24, 30);
        var value = BinaryPrimitives.ReadUInt32BigEndian(localAddress.GetAddressBytes());
        var mask = uint.MaxValue << (32 - effectivePrefix);
        var network = value & mask;
        var broadcast = network | ~mask;
        var result = new List<IPAddress>(checked((int)(broadcast - network - 1)));
        for (var candidate = network + 1; candidate < broadcast; candidate++)
        {
            if (candidate == value) continue;
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, candidate);
            result.Add(new IPAddress(bytes));
        }
        return result;
    }

    public static IPAddress GetBroadcastAddress(IPAddress localAddress, int prefixLength)
    {
        var effectivePrefix = Math.Clamp(prefixLength, 1, 30);
        var value = BinaryPrimitives.ReadUInt32BigEndian(localAddress.GetAddressBytes());
        var mask = uint.MaxValue << (32 - effectivePrefix);
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (value & mask) | ~mask);
        return new IPAddress(bytes);
    }

    private static bool IsUsable(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return !IPAddress.IsLoopback(address) && bytes[0] != 0 && bytes[0] != 169;
    }
}
