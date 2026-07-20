using PocketCam.Desktop.Updates;

namespace PocketCam.Desktop.Tests;

public sealed class ApplicationVersionTests
{
    [Theory]
    [InlineData("v0.1.2", 0, 1, 2)]
    [InlineData("V2.4", 2, 4, 0)]
    [InlineData(" 3.5.7 ", 3, 5, 7)]
    public void ParsesReleaseTags(string tagName, int major, int minor, int patch)
    {
        Assert.True(ApplicationVersion.TryParseReleaseTag(tagName, out var version));
        Assert.Equal(new Version(major, minor, patch), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1.2.3-beta.1")]
    [InlineData("v1.2.3+build")]
    public void RejectsUnsupportedReleaseTags(string tagName)
    {
        Assert.False(ApplicationVersion.TryParseReleaseTag(tagName, out _));
    }

    [Fact]
    public void FormatsOnlySemanticVersionComponents()
    {
        Assert.Equal("1.2.3", ApplicationVersion.Format(new Version(1, 2, 3, 4)));
    }
}
