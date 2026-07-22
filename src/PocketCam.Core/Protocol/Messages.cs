using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketCam.Core.Protocol;

public sealed record HelloMessage(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("appVersion")] string AppVersion,
    [property: JsonPropertyName("capabilities")] string[] Capabilities);

public sealed record StreamControl(
    [property: JsonPropertyName("stream")] bool Stream);

public sealed record StreamingMetrics(
    [property: JsonPropertyName("targetFps")] int TargetFps,
    [property: JsonPropertyName("cameraFps")] double CameraFps,
    [property: JsonPropertyName("encodedFps")] double EncodedFps,
    [property: JsonPropertyName("transmittedFps")] double TransmittedFps,
    [property: JsonPropertyName("droppedFps")] double DroppedFps);

public sealed record CameraSettings(
    [property: JsonPropertyName("width")] int Width = 1280,
    [property: JsonPropertyName("height")] int Height = 720,
    [property: JsonPropertyName("fps")] int Fps = 20,
    [property: JsonPropertyName("jpegQuality")] int JpegQuality = 80,
    [property: JsonPropertyName("lens")] string Lens = "back")
{
    public CameraSettings Validate()
    {
        if (Width is < 160 or > 3840 || Height is < 120 or > 2160)
        {
            throw new ProtocolException("Resolution is outside the supported range.");
        }

        if (Fps is < 1 or > 60)
        {
            throw new ProtocolException("FPS is outside the supported range.");
        }

        if (JpegQuality is < 20 or > 100)
        {
            throw new ProtocolException("JPEG quality is outside the supported range.");
        }

        if (Lens is not ("front" or "back"))
        {
            throw new ProtocolException("Lens must be 'front' or 'back'.");
        }

        return this;
    }
}

public static class JsonPayload
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> value) =>
        JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new ProtocolException($"Could not deserialize {typeof(T).Name}.");
}
