using PocketCam.Desktop.VirtualCamera;

namespace PocketCam.Desktop.Tests;

public sealed class VirtualCameraBackendSelectorTests
{
    [Theory]
    [InlineData(19041)]
    [InlineData(19045)]
    public void Windows10UsesDirectShow(int build)
    {
        Assert.Equal(VirtualCameraBackend.DirectShow, VirtualCameraBackendSelector.Select(new Version(10, 0, build)));
    }

    [Theory]
    [InlineData(22000)]
    [InlineData(26100)]
    public void Windows11UsesMediaFoundation(int build)
    {
        Assert.Equal(VirtualCameraBackend.MediaFoundation, VirtualCameraBackendSelector.Select(new Version(10, 0, build)));
    }

    [Fact]
    public void OlderWindowsIsUnsupported()
    {
        Assert.Equal(VirtualCameraBackend.Unsupported, VirtualCameraBackendSelector.Select(new Version(10, 0, 18363)));
    }
}
