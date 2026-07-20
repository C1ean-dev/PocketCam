using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Connections;

public sealed class UsbDiscoveryService : IEndpointDiscovery
{
    public async Task DiscoverAsync(ChannelWriter<TransportEndpoint> endpoints, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var adb = AdbLocator.Find();
            if (adb is not null)
            {
                foreach (var serial in await ListDevicesAsync(adb, cancellationToken).ConfigureAwait(false))
                {
                    var port = 20_000 + Math.Abs(StringComparer.Ordinal.GetHashCode(serial) % 10_000);
                    if (await ForwardAsync(adb, serial, port, cancellationToken).ConfigureAwait(false))
                    {
                        await endpoints.WriteAsync(
                            new TransportEndpoint(
                                $"usb:{serial}",
                                TransportKind.Usb,
                                IPAddress.Loopback.ToString(),
                                port,
                                $"Android USB ({serial})"),
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<string>> ListDevicesAsync(string adb, CancellationToken cancellationToken)
    {
        var output = await RunAsync(adb, ["devices"], cancellationToken).ConfigureAwait(false);
        return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[1] == "device")
            .Select(parts => parts[0])
            .ToArray();
    }

    private static async Task<bool> ForwardAsync(string adb, string serial, int port, CancellationToken cancellationToken)
    {
        var output = await RunAsync(
            adb,
            ["-s", serial, "forward", $"tcp:{port.ToString(CultureInfo.InvariantCulture)}", $"tcp:{ProtocolConstants.TcpPort}"],
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(output) || !output.Contains("error", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(executable)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start adb.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (await outputTask.ConfigureAwait(false)) + (await errorTask.ConfigureAwait(false));
    }
}

