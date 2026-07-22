using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Video;

public static class FrameRenderer
{
    public static BitmapSource Decode(VideoFrame frame)
    {
        using var stream = CreateStream(frame.Data);
        var decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];
        if (frame.Rotation != 0)
        {
            source = new TransformedBitmap(source, new RotateTransform(frame.Rotation));
        }
        source.Freeze();
        return source;
    }

    private static MemoryStream CreateStream(ReadOnlyMemory<byte> data)
    {
        if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array is not null)
        {
            return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false, publiclyVisible: true);
        }
        return new MemoryStream(data.ToArray(), writable: false);
    }

    public static BitmapSource ToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32) return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }
}
