using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Connections;

public sealed class UsbDiscoveryService : IEndpointDiscovery
{
    internal const int AdbServerPort = 17_892;

    public async Task DiscoverAsync(ChannelWriter<TransportEndpoint> endpoints, CancellationToken cancellationToken)
    {
        string? adb = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                adb = AdbLocator.Find();
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
        finally
        {
            if (adb is not null) await StopServerAsync(adb).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<string>> ListDevicesAsync(string adb, CancellationToken cancellationToken)
    {
        var output = await RunAsync(adb, BuildArguments(["devices"]), cancellationToken).ConfigureAwait(false);
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
            BuildArguments(["-s", serial, "forward", $"tcp:{port.ToString(CultureInfo.InvariantCulture)}", $"tcp:{ProtocolConstants.TcpPort}"]),
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(output) || !output.Contains("error", StringComparison.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> BuildArguments(IEnumerable<string> command) =>
        ["-P", AdbServerPort.ToString(CultureInfo.InvariantCulture), .. command];

    private static async Task StopServerAsync(string adb)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await RunAsync(adb, BuildArguments(["kill-server"]), timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            // The server may already have stopped or the application may be shutting down.
        }
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
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort: cancellation must still propagate to application shutdown.
            }
            throw;
        }
        return (await outputTask.ConfigureAwait(false)) + (await errorTask.ConfigureAwait(false));
    }
}
