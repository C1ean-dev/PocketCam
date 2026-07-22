namespace VCamNetSampleSourceAOT.DirectShow;

internal readonly record struct DirectShowVideoFormat(int Width, int Height, int FramesPerSecond)
{
    public int BufferSize => checked(Width * Height * 4);
    public long FrameDuration => 10_000_000L / FramesPerSecond;
    public long BitsPerSecond => BufferSize * 8L * FramesPerSecond;
    public int CapabilityBitsPerSecond => (int)Math.Min(int.MaxValue, BitsPerSecond);
}

internal static class DirectShowMediaType
{
    public static readonly DirectShowVideoFormat[] Formats =
    [
        new(1920, 1080, 60),
        new(1280, 720, 60),
        new(640, 480, 60),
        new(1920, 1080, 30),
        new(1280, 720, 30),
        new(640, 480, 30),
        new(320, 240, 15),
    ];

    public static AM_MEDIA_TYPE Create(DirectShowVideoFormat format)
    {
        var videoInfo = new VIDEOINFOHEADER
        {
            dwBitRate = checked((uint)format.BitsPerSecond),
            AvgTimePerFrame = format.FrameDuration,
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = checked((uint)Marshal.SizeOf<BITMAPINFOHEADER>()),
                biWidth = format.Width,
                // A negative height explicitly describes top-down BGRA rows.
                biHeight = -format.Height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
                biSizeImage = checked((uint)format.BufferSize),
            },
        };

        var formatSize = Marshal.SizeOf<VIDEOINFOHEADER>();
        var formatPointer = Marshal.AllocCoTaskMem(formatSize);
        Marshal.StructureToPtr(videoInfo, formatPointer, false);
        return new AM_MEDIA_TYPE
        {
            majortype = Constants.MEDIATYPE_Video,
            subtype = Constants.MEDIASUBTYPE_RGB32,
            bFixedSizeSamples = true,
            bTemporalCompression = false,
            lSampleSize = checked((uint)format.BufferSize),
            formattype = Constants.FORMAT_VideoInfo,
            pUnk = 0,
            cbFormat = checked((uint)formatSize),
            pbFormat = formatPointer,
        };
    }

    public static bool TryRead(in AM_MEDIA_TYPE mediaType, out DirectShowVideoFormat format)
    {
        format = default;
        if (mediaType.majortype != Constants.MEDIATYPE_Video ||
            mediaType.subtype != Constants.MEDIASUBTYPE_RGB32 ||
            mediaType.formattype != Constants.FORMAT_VideoInfo ||
            mediaType.pbFormat == 0 ||
            mediaType.cbFormat < Marshal.SizeOf<VIDEOINFOHEADER>())
        {
            return false;
        }

        var videoInfo = Marshal.PtrToStructure<VIDEOINFOHEADER>(mediaType.pbFormat);
        var height = Math.Abs(videoInfo.bmiHeader.biHeight);
        var fps = videoInfo.AvgTimePerFrame > 0
            ? checked((int)Math.Round(10_000_000d / videoInfo.AvgTimePerFrame))
            : 30;
        foreach (var candidate in Formats)
        {
            if (candidate.Width == videoInfo.bmiHeader.biWidth &&
                candidate.Height == height &&
                candidate.FramesPerSecond == fps &&
                videoInfo.bmiHeader.biBitCount == 32 &&
                videoInfo.bmiHeader.biCompression == 0)
            {
                format = candidate;
                return true;
            }
        }
        return false;
    }

    public static nint Allocate(DirectShowVideoFormat format)
    {
        var mediaType = Create(format);
        var pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<AM_MEDIA_TYPE>());
        Marshal.StructureToPtr(mediaType, pointer, false);
        return pointer;
    }
}
