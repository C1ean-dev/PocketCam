namespace PocketCam.Desktop.Updates;

public sealed record ReleaseUpdate(
    Version Version,
    string TagName,
    Uri ReleasePageUri,
    Uri? WindowsDownloadUri);
