using PocketCam.Core.Connections;

namespace PocketCam.Desktop.Connections;

public sealed record TransportEndpoint(
    string Id,
    TransportKind Kind,
    string Address,
    int Port,
    string DeviceName,
    string? ExpectedDeviceId = null);

