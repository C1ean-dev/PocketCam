using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PocketCam.Desktop.Updates;

public sealed class GitHubReleaseUpdateService : IDisposable
{
    public const string WindowsAssetName = "PocketCam-Windows-win-x64.zip";

    private static readonly Uri LatestReleaseEndpoint =
        new("https://api.github.com/repos/C1ean-dev/PocketCam/releases/latest");

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GitHubReleaseUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _ownsHttpClient = httpClient is null;
    }

    public async Task<ReleaseUpdate?> FindUpdateAsync(Version installedVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd($"PocketCam/{ApplicationVersion.Format(installedVersion)}");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken).ConfigureAwait(false);
        if (release is null || release.Draft || release.Prerelease) return null;
        if (!ApplicationVersion.TryParseReleaseTag(release.TagName, out var availableVersion)) return null;
        if (availableVersion <= ApplicationVersion.Normalize(installedVersion)) return null;
        if (!TryCreateHttpsUri(release.HtmlUrl, out var releasePageUri) || releasePageUri is null) return null;

        var downloadUrl = release.Assets?
            .FirstOrDefault(asset => string.Equals(asset.Name, WindowsAssetName, StringComparison.OrdinalIgnoreCase))?
            .BrowserDownloadUrl;
        TryCreateHttpsUri(downloadUrl, out var windowsDownloadUri);

        return new ReleaseUpdate(availableVersion, release.TagName!, releasePageUri, windowsDownloadUri);
    }

    private static bool TryCreateHttpsUri(string? value, out Uri? uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate) && candidate.Scheme == Uri.UriSchemeHttps)
        {
            uri = candidate;
            return true;
        }

        uri = null;
        return false;
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);
}
