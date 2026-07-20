using System.Globalization;

namespace PocketCam.Desktop.Updates;

public static class ApplicationVersion
{
    public static Version Current => Normalize(typeof(ApplicationVersion).Assembly.GetName().Version);

    public static string Format(Version version)
    {
        var normalized = Normalize(version);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{normalized.Major}.{normalized.Minor}.{normalized.Build}");
    }

    public static bool TryParseReleaseTag(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName)) return false;

        var candidate = tagName.Trim();
        if (candidate.StartsWith('v') || candidate.StartsWith('V')) candidate = candidate[1..];
        if (candidate.Contains('-', StringComparison.Ordinal) || candidate.Contains('+', StringComparison.Ordinal)) return false;
        if (!Version.TryParse(candidate, out var parsed) || parsed.Major < 0 || parsed.Minor < 0) return false;

        version = Normalize(parsed);
        return true;
    }

    public static Version Normalize(Version? version) => version is null
        ? new Version(0, 0, 0)
        : new Version(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0));
}
