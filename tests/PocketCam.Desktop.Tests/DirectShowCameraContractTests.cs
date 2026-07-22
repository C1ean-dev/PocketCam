using System.Runtime.InteropServices;
using DirectN;
using DirectN.Extensions.Com;
using VCamNetSampleSourceAOT.DirectShow;

namespace PocketCam.Desktop.Tests;

public sealed class DirectShowCameraContractTests
{
    [Fact]
    public void CapturePinEnumeratesRgb32Formats()
    {
        using var filter = new DirectShowCameraFilter();
        Assert.True(filter.EnumPins(out var enumerator).IsSuccess);

        var pinPointers = new nint[1];
        Assert.True(enumerator.Next(1, pinPointers, 0).IsSuccess);
        Assert.NotEqual(0, pinPointers[0]);

        using var pin = ComObject.FromPointer<IPin>(pinPointers[0]);
        Assert.NotNull(pin);
        Assert.True(pin.Object.QueryDirection(out var direction).IsSuccess);
        Assert.Equal(PIN_DIRECTION.PINDIR_OUTPUT, direction);

        var configPointer = ComObject.QueryInterface<IAMStreamConfig>(pinPointers[0], false);
        Assert.True(configPointer != 0);
        using var config = ComObject.FromPointer<IAMStreamConfig>(configPointer);
        Assert.NotNull(config);
        Assert.True(config.Object.GetNumberOfCapabilities(out var count, out var capabilitySize).IsSuccess);
        Assert.Equal(7, count);
        Assert.Equal(Marshal.SizeOf<VIDEO_STREAM_CONFIG_CAPS>(), capabilitySize);

        var capabilities = Marshal.AllocCoTaskMem(capabilitySize);
        try
        {
            Assert.True(config.Object.GetStreamCaps(1, out var mediaTypePointer, capabilities).IsSuccess);
            try
            {
                var mediaType = Marshal.PtrToStructure<AM_MEDIA_TYPE>(mediaTypePointer);
                Assert.Equal(Constants.MEDIATYPE_Video, mediaType.majortype);
                Assert.Equal(Constants.MEDIASUBTYPE_RGB32, mediaType.subtype);
                Assert.Equal(Constants.FORMAT_VideoInfo, mediaType.formattype);
                var videoInfo = Marshal.PtrToStructure<VIDEOINFOHEADER>(mediaType.pbFormat);
                Assert.Equal(1280, videoInfo.bmiHeader.biWidth);
                Assert.Equal(-720, videoInfo.bmiHeader.biHeight);
                Assert.Equal(32, videoInfo.bmiHeader.biBitCount);
                Assert.Equal(60, (int)Math.Round(10_000_000d / videoInfo.AvgTimePerFrame));
                Marshal.FreeCoTaskMem(mediaType.pbFormat);
            }
            finally
            {
                Marshal.FreeCoTaskMem(mediaTypePointer);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(capabilities);
        }
    }

    [Fact]
    public void CapturePinDeliversSamplesToDirectShowConsumer()
    {
        using var filter = new DirectShowCameraFilter();
        Assert.True(filter.EnumPins(out var enumerator).IsSuccess);
        var pinPointers = new nint[1];
        Assert.True(enumerator.Next(1, pinPointers, 0).IsSuccess);
        using var pin = ComObject.FromPointer<IPin>(pinPointers[0]);
        Assert.NotNull(pin);

        var configPointer = ComObject.QueryInterface<IAMStreamConfig>(pinPointers[0], false);
        using var config = ComObject.FromPointer<IAMStreamConfig>(configPointer);
        Assert.NotNull(config);
        var capabilitySize = Marshal.SizeOf<VIDEO_STREAM_CONFIG_CAPS>();
        var capabilities = Marshal.AllocCoTaskMem(capabilitySize);
        try
        {
            Assert.True(config.Object.GetStreamCaps(6, out var mediaTypePointer, capabilities).IsSuccess);
            try
            {
                var consumer = new TestInputPin();
                Assert.True(pin.Object.Connect(consumer, mediaTypePointer).IsSuccess);
                Assert.True(filter.Run(0).IsSuccess);
                Assert.True(consumer.WaitForFrame(TimeSpan.FromSeconds(3)));
                Assert.True(filter.Stop().IsSuccess);
                Assert.True(pin.Object.Disconnect().IsSuccess);
                Assert.Equal(320 * 240 * 4, consumer.FrameLength);
            }
            finally
            {
                var mediaType = Marshal.PtrToStructure<AM_MEDIA_TYPE>(mediaTypePointer);
                Marshal.FreeCoTaskMem(mediaType.pbFormat);
                Marshal.FreeCoTaskMem(mediaTypePointer);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(capabilities);
        }
    }
}

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
internal sealed partial class TestInputPin : IPin, IMemInputPin
{
    private readonly ManualResetEventSlim _received = new(false);

    public int FrameLength { get; private set; }

    public bool WaitForFrame(TimeSpan timeout) => _received.Wait(timeout);

    public HRESULT Connect(IPin receivePin, nint mediaType) => Constants.E_UNEXPECTED;
    public HRESULT ReceiveConnection(IPin connector, in AM_MEDIA_TYPE mediaType) => Constants.S_OK;
    public HRESULT Disconnect() => Constants.S_OK;

    public HRESULT ConnectedTo(out IPin pin)
    {
        pin = null!;
        return unchecked((int)0x80040209);
    }

    public HRESULT ConnectionMediaType(out AM_MEDIA_TYPE mediaType)
    {
        mediaType = default;
        return unchecked((int)0x80040209);
    }

    public HRESULT QueryPinInfo(out PIN_INFO info)
    {
        info = default;
        info.dir = PIN_DIRECTION.PINDIR_INPUT;
        return Constants.S_OK;
    }

    public HRESULT QueryDirection(out PIN_DIRECTION direction)
    {
        direction = PIN_DIRECTION.PINDIR_INPUT;
        return Constants.S_OK;
    }

    public HRESULT QueryId(out PWSTR id)
    {
        id = PWSTR.From("Input");
        return Constants.S_OK;
    }

    public HRESULT QueryAccept(in AM_MEDIA_TYPE mediaType) => Constants.S_OK;

    public HRESULT EnumMediaTypes(out IEnumMediaTypes enumerator)
    {
        enumerator = null!;
        return Constants.E_NOTIMPL;
    }

    public HRESULT QueryInternalConnections(nint pins, ref uint count)
    {
        count = 0;
        return Constants.E_NOTIMPL;
    }

    public HRESULT EndOfStream() => Constants.S_OK;
    public HRESULT BeginFlush() => Constants.S_OK;
    public HRESULT EndFlush() => Constants.S_OK;
    public HRESULT NewSegment(long start, long stop, double rate) => Constants.S_OK;

    public HRESULT GetAllocator(out IMemAllocator allocator)
    {
        allocator = null!;
        return Constants.E_NOTIMPL;
    }

    public HRESULT NotifyAllocator(IMemAllocator allocator, BOOL readOnly) => Constants.S_OK;

    public HRESULT GetAllocatorRequirements(out ALLOCATOR_PROPERTIES properties)
    {
        properties = default;
        return Constants.E_NOTIMPL;
    }

    public HRESULT Receive(IMediaSample sample)
    {
        FrameLength = sample.GetActualDataLength();
        _received.Set();
        return Constants.S_OK;
    }

    public HRESULT ReceiveMultiple(IMediaSample[] samples, int sampleCount, out int processed)
    {
        processed = 0;
        foreach (var sample in samples.Take(sampleCount))
        {
            if (Receive(sample).IsError) break;
            processed++;
        }
        return processed == sampleCount ? Constants.S_OK : Constants.S_FALSE;
    }

    public HRESULT ReceiveCanBlock() => Constants.S_FALSE;
}
