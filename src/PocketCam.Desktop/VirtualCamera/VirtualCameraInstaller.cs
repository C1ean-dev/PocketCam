using System.Diagnostics;

namespace PocketCam.Desktop.VirtualCamera;

public static class VirtualCameraInstaller
{
    private static Process? _host;
    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    public static string StatusText => IsSupported
        ? "A saída é publicada para a câmera virtual PocketCam no Windows 11."
        : "A câmera virtual exige Windows 11; o preview e as conexões continuam disponíveis.";

    public static async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported) throw new PlatformNotSupportedException("A câmera virtual exige Windows 11 build 22000 ou superior.");
        Stop();
        var register = Path.Combine(AppContext.BaseDirectory, "virtual-camera", "PocketCam.VirtualCamera.Host.exe");
        if (!File.Exists(register))
        {
            throw new FileNotFoundException("O componente da câmera virtual não foi incluído neste pacote.", register);
        }
        var info = new ProcessStartInfo(register)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("install");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Não foi possível iniciar o instalador da câmera virtual.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0) throw new InvalidOperationException($"O instalador retornou o código {process.ExitCode}.");
        Start();
    }

    public static bool Start()
    {
        if (!IsSupported || _host is { HasExited: false }) return _host is { HasExited: false };
        var host = Path.Combine(AppContext.BaseDirectory, "virtual-camera", "PocketCam.VirtualCamera.Host.exe");
        if (!File.Exists(host)) return false;
        var info = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        info.ArgumentList.Add("run");
        info.ArgumentList.Add("--parent");
        info.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _host = Process.Start(info);
        return _host is not null;
    }

    public static void Stop()
    {
        if (_host is null) return;
        if (!_host.HasExited) _host.Kill(entireProcessTree: true);
        _host.Dispose();
        _host = null;
    }
}
