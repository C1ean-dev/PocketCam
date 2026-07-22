using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PocketCam.Core.Protocol;
using PocketCam.Desktop.Video;
using Xunit.Abstractions;

namespace PocketCam.Desktop.Tests;

public sealed class FramePipelinePerformanceTests
{
    private readonly ITestOutputHelper _output;

    public FramePipelinePerformanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Performance")]
    public void VgaJpegDecodeAndSharedMemoryCopySustainSixtyFramesPerSecond()
    {
        const int width = 640;
        const int height = 480;
        const int frameCount = 120;
        var jpeg = CreateRepresentativeJpeg(width, height);
        var frame = new VideoFrame(width, height, 0, VideoCodec.Jpeg, jpeg);
        var pixels = new byte[width * height * 4];
        using var mapping = MemoryMappedFile.CreateNew(null, pixels.Length + SharedMemoryFrameSink.HeaderSize);
        using var view = mapping.CreateViewAccessor();

        ProcessFrame(frame, pixels, view); // Warm up WPF codecs and JIT before measuring.
        var started = Stopwatch.GetTimestamp();
        for (var index = 0; index < frameCount; index++)
        {
            ProcessFrame(frame, pixels, view);
        }
        var elapsed = Stopwatch.GetElapsedTime(started);
        var framesPerSecond = frameCount / elapsed.TotalSeconds;
        _output.WriteLine($"{framesPerSecond:F1} FPS; JPEG {jpeg.Length:N0} bytes; {elapsed.TotalMilliseconds:F0} ms for {frameCount} frames.");

        Assert.True(
            framesPerSecond >= 60,
            $"Desktop frame pipeline reached only {framesPerSecond:F1} FPS ({jpeg.Length:N0}-byte JPEG).");
    }

    private static void ProcessFrame(VideoFrame frame, byte[] pixels, MemoryMappedViewAccessor view)
    {
        var bitmap = FrameRenderer.ToBgra32(FrameRenderer.Decode(frame));
        var stride = bitmap.PixelWidth * 4;
        bitmap.CopyPixels(pixels, stride, 0);
        view.WriteArray(SharedMemoryFrameSink.HeaderSize, pixels, 0, pixels.Length);
    }

    private static byte[] CreateRepresentativeJpeg(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var random = new Random(20260721);
        random.NextBytes(pixels);
        for (var alpha = 3; alpha < pixels.Length; alpha += 4) pixels[alpha] = 0xff;

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        var encoder = new JpegBitmapEncoder { QualityLevel = 40 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
