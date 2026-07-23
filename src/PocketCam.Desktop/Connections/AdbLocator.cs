namespace PocketCam.Desktop.Connections;

public static class AdbLocator
{
    private static readonly object Gate = new();
    private static string? _cached;

    public static string? Find()
    {
        lock (Gate)
        {
            if (_cached is not null && File.Exists(_cached)) return _cached;

            var candidates = new List<string>();
            AddSdkCandidate(candidates, Environment.GetEnvironmentVariable("ANDROID_HOME"));
            AddSdkCandidate(candidates, Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"));

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                candidates.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"));
            }

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(folder => Path.Combine(folder.Trim().Trim('"'), "adb.exe")));
            _cached = candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(File.Exists);
            if (_cached is not null) return _cached;

            var bundled = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe"),
                Path.Combine(AppContext.BaseDirectory, "adb.exe"),
            }.FirstOrDefault(File.Exists);
            if (bundled is null) return null;

            var cacheBase = string.IsNullOrWhiteSpace(localAppData) ? Path.GetTempPath() : localAppData;
            try
            {
                _cached = AdbToolCache.Stage(bundled, Path.Combine(cacheBase, "PocketCam", "adb"));
            }
            catch
            {
                // Never execute from the extracted application directory: doing so would
                // keep its tools folder locked after the main window closes or updates.
                _cached = null;
            }
            return _cached;
        }
    }

    private static void AddSdkCandidate(ICollection<string> candidates, string? sdkRoot)
    {
        if (!string.IsNullOrWhiteSpace(sdkRoot))
        {
            candidates.Add(Path.Combine(sdkRoot.Trim().Trim('"'), "platform-tools", "adb.exe"));
        }
    }
}
