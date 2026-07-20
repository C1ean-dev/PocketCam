using System.Net;
using System.Net.Http;
using System.Text;
using PocketCam.Desktop.Updates;

namespace PocketCam.Desktop.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Fact]
    public async Task ReturnsNewerReleaseAndWindowsAsset()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
                {
                  "tag_name": "v0.2.0",
                  "html_url": "https://github.com/C1ean-dev/PocketCam/releases/tag/v0.2.0",
                  "draft": false,
                  "prerelease": false,
                  "assets": [
                    {
                      "name": "PocketCam-Windows-win-x64.zip",
                      "browser_download_url": "https://github.com/C1ean-dev/PocketCam/releases/download/v0.2.0/PocketCam-Windows-win-x64.zip"
                    }
                  ]
                }
                """);
        }));
        using var service = new GitHubReleaseUpdateService(client);

        var update = await service.FindUpdateAsync(new Version(0, 1, 2));

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 2, 0), update.Version);
        Assert.Equal(GitHubReleaseUpdateService.WindowsAssetName, update.WindowsDownloadUri!.Segments[^1]);
        Assert.Equal("application/vnd.github+json", capturedRequest!.Headers.Accept.Single().MediaType);
        Assert.Equal("2026-03-10", capturedRequest.Headers.GetValues("X-GitHub-Api-Version").Single());
        Assert.Equal("PocketCam/0.1.2", capturedRequest.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData("v0.1.2")]
    [InlineData("v0.1.1")]
    public async Task IgnoresCurrentOrOlderRelease(string tagName)
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse($$"""
            {
              "tag_name": "{{tagName}}",
              "html_url": "https://github.com/C1ean-dev/PocketCam/releases/tag/{{tagName}}",
              "draft": false,
              "prerelease": false,
              "assets": []
            }
            """)));
        using var service = new GitHubReleaseUpdateService(client);

        Assert.Null(await service.FindUpdateAsync(new Version(0, 1, 2)));
    }

    [Fact]
    public async Task IgnoresPrereleaseResponse()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "tag_name": "v0.3.0-beta.1",
              "html_url": "https://github.com/C1ean-dev/PocketCam/releases/tag/v0.3.0-beta.1",
              "draft": false,
              "prerelease": true,
              "assets": []
            }
            """)));
        using var service = new GitHubReleaseUpdateService(client);

        Assert.Null(await service.FindUpdateAsync(new Version(0, 1, 2)));
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }
}
