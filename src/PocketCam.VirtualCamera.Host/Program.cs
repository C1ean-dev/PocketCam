using System.Diagnostics;
using System.Runtime.InteropServices;
using DirectN;

namespace PocketCam.VirtualCamera.Host;

internal static class Program
{
    private const string SourceClsid = "f7f3ca82-21d4-4aba-b2e1-bc0109e2e83d";

    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) return 20;
        if (args.Length == 0) return 2;
        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "install" => Register(uninstall: false),
                "uninstall" => Register(uninstall: true),
                "run" => Run(args),
                _ => 2,
            };
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"PocketCam Virtual Camera: {error}");
            return 1;
        }
    }

    private static int Register(bool uninstall)
    {
        var packagedSource = Path.Combine(AppContext.BaseDirectory, "PocketCam.VirtualCamera.Source.dll");
        var installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PocketCam",
            "VirtualCamera");
        var installedSource = Path.Combine(installDirectory, "PocketCam.VirtualCamera.Source.dll");

        if (!uninstall)
        {
            if (!File.Exists(packagedSource)) return 3;
            Directory.CreateDirectory(installDirectory);
            File.Copy(packagedSource, installedSource, overwrite: true);
        }
        else if (!File.Exists(installedSource))
        {
            return 0;
        }

        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "regsvr32.exe"))
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"/s {(uninstall ? "/u " : string.Empty)}\"{installedSource}\"",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        using var process = Process.Start(info);
        if (process is null) return 4;
        process.WaitForExit();
        if (uninstall && process.ExitCode == 0)
        {
            try { File.Delete(installedSource); }
            catch (IOException) { }
        }
        return process.ExitCode;
    }

    private static int Run(IReadOnlyList<string> args)
    {
        // On Windows 10 the camera is a persistent DirectShow capture filter
        // loaded by each consumer, so there is no separate host process.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return 0;

        MFFunctions.MFStartup();
        try
        {
            var hr = Functions.MFCreateVirtualCamera(
                __MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0001.MFVirtualCameraType_SoftwareCameraSource,
                __MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0002.MFVirtualCameraLifetime_Session,
                __MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0003.MFVirtualCameraAccess_CurrentUser,
                "PocketCam",
                $"{{{SourceClsid}}}",
                null,
                0,
                out var camera);
            if (hr.IsError) return 5;
            using var virtualCamera = new ComObject<IMFVirtualCamera>(camera);
            hr = virtualCamera.Object.Start(null);
            if (hr.IsError) return 6;

            var parentIndex = args.ToList().FindIndex(value => value == "--parent");
            if (parentIndex >= 0 && parentIndex + 1 < args.Count && int.TryParse(args[parentIndex + 1], out var parentId))
            {
                try { Process.GetProcessById(parentId).WaitForExit(); }
                catch (ArgumentException) { }
            }
            else
            {
                using var stopped = new ManualResetEventSlim();
                Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stopped.Set(); };
                stopped.Wait();
            }

            virtualCamera.Object.Remove();
            return 0;
        }
        finally
        {
            MFFunctions.MFShutdown();
        }
    }
}
