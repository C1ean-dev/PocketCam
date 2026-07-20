using System.Diagnostics;

namespace PocketCam.Desktop.VirtualCamera;

public static class VirtualCameraInstaller
{
    private static Process? _host;
    public static VirtualCameraBackend Backend => VirtualCameraBackendSelector.Current;
    public static bool IsSupported => Backend is not VirtualCameraBackend.Unsupported;

    public static string StatusText => Backend switch
    {
        VirtualCameraBackend.MediaFoundation => "Câmera virtual Media Foundation disponível no Windows 11.",
        VirtualCameraBackend.DirectShow => "Câmera virtual DirectShow disponível no Windows 10.",
        _ => "A câmera virtual exige Windows 10 versão 2004 ou superior.",
    };

    public static async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported) throw new PlatformNotSupportedException("A câmera virtual exige Windows 10 versão 2004 (build 19041) ou superior.");
        Stop();
        var register = Path.Combine(AppContext.BaseDirectory, "virtual-camera", "PocketCam.VirtualCamera.Host.exe");
        if (!File.Exists(register))
        {
            throw new FileNotFoundException("O componente da câmera virtual não foi incluído neste pacote.", register);
        }
        var info = new ProcessStartInfo(register)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        info.ArgumentList.Add("install");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Não foi possível iniciar o instalador da câmera virtual.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0) throw new InvalidOperationException($"O instalador retornou o código {process.ExitCode}.");
        Start();
    }

    public static bool Start()
    {
        if (!IsSupported) return false;
        // DirectShow loads the source DLL inside the consuming application and
        // therefore does not need a separate lifetime host on Windows 10.
        if (Backend == VirtualCameraBackend.DirectShow) return true;
        if (_host is { HasExited: false }) return true;
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
