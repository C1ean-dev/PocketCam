using System.Security.Cryptography;
using System.Text;

namespace PocketCam.Desktop.Connections;

internal static class AdbToolCache
{
    private static readonly string[] RuntimeFiles = ["adb.exe", "AdbWinApi.dll", "AdbWinUsbApi.dll"];

    public static string Stage(string bundledAdb, string cacheRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundledAdb);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);

        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(bundledAdb))
            ?? throw new InvalidOperationException("ADB directory could not be resolved.");
        var sourceFiles = RuntimeFiles
            .Select(name => Path.Combine(sourceDirectory, name))
            .ToArray();
        if (sourceFiles.Any(path => !File.Exists(path)))
        {
            throw new FileNotFoundException("The bundled ADB runtime is incomplete.");
        }

        var fingerprint = ComputeFingerprint(sourceFiles);
        var destinationDirectory = Path.Combine(Path.GetFullPath(cacheRoot), fingerprint);
        Directory.CreateDirectory(destinationDirectory);
        foreach (var source in sourceFiles)
        {
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
            if (File.Exists(destination)) continue;
            try
            {
                File.Copy(source, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                // Another PocketCam process completed the same content-addressed cache.
            }
        }

        return Path.Combine(destinationDirectory, "adb.exe");
    }

    private static string ComputeFingerprint(IEnumerable<string> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(file)));
            using var input = File.OpenRead(file);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()[..16];
    }
}
