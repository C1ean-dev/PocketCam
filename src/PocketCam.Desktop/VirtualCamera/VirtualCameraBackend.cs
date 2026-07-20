namespace PocketCam.Desktop.VirtualCamera;

public enum VirtualCameraBackend
{
    Unsupported,
    DirectShow,
    MediaFoundation,
}

public static class VirtualCameraBackendSelector
{
    public static VirtualCameraBackend Current =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? VirtualCameraBackend.MediaFoundation
            : OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)
                ? VirtualCameraBackend.DirectShow
                : VirtualCameraBackend.Unsupported;

    public static VirtualCameraBackend Select(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Major > 10 || version.Major == 10 && version.Build >= 22000)
            return VirtualCameraBackend.MediaFoundation;
        if (version.Major == 10 && version.Build >= 19041)
            return VirtualCameraBackend.DirectShow;
        return VirtualCameraBackend.Unsupported;
    }
}
