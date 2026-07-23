using PocketCam.Desktop.Connections;

namespace PocketCam.Desktop.Tests;

public sealed class AdbLifecycleTests
{
    [Fact]
    public void StagesBundledRuntimeOutsideTheExtractedApplicationDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pocketcam-adb-test-{Guid.NewGuid():N}");
        var bundled = Path.Combine(root, "extracted-app", "tools");
        var cache = Path.Combine(root, "local-cache");
        Directory.CreateDirectory(bundled);
        File.WriteAllBytes(Path.Combine(bundled, "adb.exe"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(bundled, "AdbWinApi.dll"), [4, 5]);
        File.WriteAllBytes(Path.Combine(bundled, "AdbWinUsbApi.dll"), [6, 7]);

        try
        {
            var staged = AdbToolCache.Stage(Path.Combine(bundled, "adb.exe"), cache);
            var stagedAgain = AdbToolCache.Stage(Path.Combine(bundled, "adb.exe"), cache);

            Assert.Equal(staged, stagedAgain);
            Assert.StartsWith(Path.GetFullPath(cache), staged, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Path.GetFullPath(bundled), staged, StringComparison.OrdinalIgnoreCase);
            Assert.Equal([1, 2, 3], File.ReadAllBytes(staged));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(staged)!, "AdbWinApi.dll")));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(staged)!, "AdbWinUsbApi.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UsesDedicatedPocketCamServerPortForEveryAdbCommand()
    {
        Assert.Equal(
            ["-P", "17892", "devices"],
            UsbDiscoveryService.BuildArguments(["devices"]));
    }
}
