using System.IO.MemoryMappedFiles;
using System.Windows.Media.Imaging;

namespace PocketCam.Desktop.Video;

public sealed class SharedMemoryFrameSink : IDisposable
{
    public const int HeaderSize = 64;
    public const int Capacity = HeaderSize + (3840 * 2160 * 4);
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PocketCam",
        "frames.v1");

    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _view;
    private int _sequence;

    public SharedMemoryFrameSink()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        _mapping = MemoryMappedFile.CreateFromFile(
            FilePath,
            FileMode.OpenOrCreate,
            mapName: null,
            Capacity,
            MemoryMappedFileAccess.ReadWrite);
        _view = _mapping.CreateViewAccessor(0, Capacity, MemoryMappedFileAccess.ReadWrite);
        _view.WriteArray(0, "PCVF"u8.ToArray(), 0, 4);
        _view.Write(4, 1);
        _view.Write(8, HeaderSize);
    }

    public void Publish(BitmapSource source, long timestampMicroseconds)
    {
        var stride = checked(source.PixelWidth * 4);
        var byteCount = checked(stride * source.PixelHeight);
        if (byteCount > Capacity - HeaderSize) return;
        var pixels = new byte[byteCount];
        source.CopyPixels(pixels, stride, 0);

        var writing = Interlocked.Add(ref _sequence, 2) - 1;
        _view.Write(12, writing); // Odd means a writer is active.
        _view.Write(16, source.PixelWidth);
        _view.Write(20, source.PixelHeight);
        _view.Write(24, stride);
        _view.Write(28, byteCount);
        _view.Write(32, timestampMicroseconds);
        _view.WriteArray(HeaderSize, pixels, 0, pixels.Length);
        _view.Write(12, writing + 1); // Even means a complete frame.
        _view.Flush();
    }

    public void Dispose()
    {
        _view.Dispose();
        _mapping.Dispose();
    }
}
