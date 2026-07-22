namespace VCamNetSampleSourceAOT.DirectShow;

[GeneratedComClass]
internal sealed partial class DirectShowCameraPin : IPin, IAMStreamConfig, IKsPropertySet, IDisposable
{
    public const string Id = "Capture";
    private const uint KsPropertySupportGet = 1;
    private const uint AmPropertyPinCategory = 0;

    private readonly object _sync = new();
    private readonly DirectShowCameraFilter _filter;
    private readonly SharedFrameReader _frameReader = new();
    private IComObject<IPin>? _connected;
    private IComObject<IMemInputPin>? _input;
    private IComObject<IMemAllocator>? _allocator;
    private DirectShowVideoFormat _format = DirectShowMediaType.Formats[1];
    private ManualResetEventSlim? _stopSignal;
    private Thread? _streamThread;
    private bool _disposed;

    public DirectShowCameraPin(DirectShowCameraFilter filter)
    {
        _filter = filter;
    }

    public HRESULT Connect(IPin receivePin, nint mediaTypePointer)
    {
        if (receivePin is null) return Constants.E_POINTER;
        lock (_sync)
        {
            if (_connected is not null) return DirectShowHResults.AlreadyConnected;
            if (_filter.State != FILTER_STATE.State_Stopped) return DirectShowHResults.NotStopped;

            if (mediaTypePointer != 0)
            {
                var requested = Marshal.PtrToStructure<AM_MEDIA_TYPE>(mediaTypePointer);
                if (!DirectShowMediaType.TryRead(requested, out var requestedFormat))
                    return DirectShowHResults.TypeNotAccepted;
                return TryConnect(receivePin, requestedFormat);
            }

            foreach (var candidate in DirectShowMediaType.Formats)
            {
                var result = TryConnect(receivePin, candidate);
                if (result.IsSuccess) return result;
            }
            return DirectShowHResults.NoAcceptableTypes;
        }
    }

    private HRESULT TryConnect(IPin receivePin, DirectShowVideoFormat format)
    {
        var mediaType = DirectShowMediaType.Create(format);
        try
        {
            var result = receivePin.ReceiveConnection(this, mediaType);
            if (result.IsError) return result;

            var connected = Retain<IPin>(receivePin);
            var input = Retain<IMemInputPin>(receivePin);
            if (connected is null || input is null)
            {
                connected.SafeDispose();
                input.SafeDispose();
                receivePin.Disconnect();
                return Constants.E_NOINTERFACE;
            }

            var allocator = DirectN.Extensions.Com.ComObject.CoCreate<IMemAllocator>(Constants.CLSID_MemoryAllocator);
            if (allocator is null)
            {
                connected.Dispose();
                input.Dispose();
                receivePin.Disconnect();
                return Constants.E_FAIL;
            }

            var requestedProperties = new ALLOCATOR_PROPERTIES
            {
                cBuffers = 3,
                cbBuffer = format.BufferSize,
                cbAlign = 1,
                cbPrefix = 0,
            };
            result = allocator.Object.SetProperties(requestedProperties, out var actualProperties);
            if (result.IsError || actualProperties.cbBuffer < format.BufferSize)
            {
                allocator.Dispose();
                connected.Dispose();
                input.Dispose();
                receivePin.Disconnect();
                return result.IsError ? result : Constants.E_FAIL;
            }

            result = input.Object.NotifyAllocator(allocator.Object, false);
            if (result.IsError)
            {
                allocator.Dispose();
                connected.Dispose();
                input.Dispose();
                receivePin.Disconnect();
                return result;
            }

            _format = format;
            _connected = connected;
            _input = input;
            _allocator = allocator;
            return Constants.S_OK;
        }
        finally
        {
            if (mediaType.pbFormat != 0) Marshal.FreeCoTaskMem(mediaType.pbFormat);
        }
    }

    public HRESULT ReceiveConnection(IPin connector, in AM_MEDIA_TYPE mediaType) => Constants.E_UNEXPECTED;

    public HRESULT Disconnect()
    {
        lock (_sync)
        {
            if (_connected is null) return Constants.S_FALSE;
            if (_filter.State != FILTER_STATE.State_Stopped) return DirectShowHResults.NotStopped;
            StopStreaming();
            ReleaseConnection();
            return Constants.S_OK;
        }
    }

    public HRESULT ConnectedTo(out IPin pin)
    {
        lock (_sync)
        {
            if (_connected is null)
            {
                pin = null!;
                return DirectShowHResults.NotConnected;
            }
            pin = _connected.Object;
            return Constants.S_OK;
        }
    }

    public HRESULT ConnectionMediaType(out AM_MEDIA_TYPE mediaType)
    {
        lock (_sync)
        {
            if (_connected is null)
            {
                mediaType = default;
                return DirectShowHResults.NotConnected;
            }
            mediaType = DirectShowMediaType.Create(_format);
            return Constants.S_OK;
        }
    }

    public HRESULT QueryPinInfo(out PIN_INFO info)
    {
        info = default;
        info.pFilter = DirectN.Extensions.Com.ComObject.GetOrCreateComInstance<IBaseFilter>(_filter);
        info.dir = PIN_DIRECTION.PINDIR_OUTPUT;
        CopyName(ref info.achName, "Video");
        return Constants.S_OK;
    }

    public HRESULT QueryDirection(out PIN_DIRECTION direction)
    {
        direction = PIN_DIRECTION.PINDIR_OUTPUT;
        return Constants.S_OK;
    }

    public HRESULT QueryId(out PWSTR id)
    {
        id = PWSTR.From(Id);
        return Constants.S_OK;
    }

    public HRESULT QueryAccept(in AM_MEDIA_TYPE mediaType) =>
        DirectShowMediaType.TryRead(mediaType, out _) ? Constants.S_OK : Constants.S_FALSE;

    public HRESULT EnumMediaTypes(out IEnumMediaTypes enumerator)
    {
        enumerator = new DirectShowMediaTypeEnumerator();
        return Constants.S_OK;
    }

    public HRESULT QueryInternalConnections(nint pins, ref uint count)
    {
        count = 0;
        return Constants.E_NOTIMPL;
    }

    public HRESULT EndOfStream()
    {
        lock (_sync) return _connected?.Object.EndOfStream() ?? DirectShowHResults.NotConnected;
    }

    public HRESULT BeginFlush()
    {
        lock (_sync) return _connected?.Object.BeginFlush() ?? DirectShowHResults.NotConnected;
    }

    public HRESULT EndFlush()
    {
        lock (_sync) return _connected?.Object.EndFlush() ?? DirectShowHResults.NotConnected;
    }

    public HRESULT NewSegment(long start, long stop, double rate)
    {
        lock (_sync) return _connected?.Object.NewSegment(start, stop, rate) ?? DirectShowHResults.NotConnected;
    }

    internal void ForwardNewSegment()
    {
        lock (_sync) _connected?.Object.NewSegment(0, long.MaxValue, 1);
    }

    public HRESULT SetFormat(in AM_MEDIA_TYPE mediaType)
    {
        lock (_sync)
        {
            if (_connected is not null || _filter.State != FILTER_STATE.State_Stopped)
                return DirectShowHResults.NotStopped;
            if (!DirectShowMediaType.TryRead(mediaType, out var format))
                return DirectShowHResults.TypeNotAccepted;
            _format = format;
            return Constants.S_OK;
        }
    }

    public HRESULT GetFormat(out nint mediaType)
    {
        lock (_sync) mediaType = DirectShowMediaType.Allocate(_format);
        return Constants.S_OK;
    }

    public HRESULT GetNumberOfCapabilities(out int count, out int size)
    {
        count = DirectShowMediaType.Formats.Length;
        size = Marshal.SizeOf<VIDEO_STREAM_CONFIG_CAPS>();
        return Constants.S_OK;
    }

    public HRESULT GetStreamCaps(int index, out nint mediaType, nint capabilitiesPointer)
    {
        mediaType = 0;
        if (index < 0 || index >= DirectShowMediaType.Formats.Length) return Constants.E_INVALIDARG;
        if (capabilitiesPointer == 0) return Constants.E_POINTER;

        var format = DirectShowMediaType.Formats[index];
        mediaType = DirectShowMediaType.Allocate(format);
        var size = new SIZE { cx = format.Width, cy = format.Height };
        var caps = new VIDEO_STREAM_CONFIG_CAPS
        {
            guid = Constants.FORMAT_VideoInfo,
            InputSize = size,
            MinCroppingSize = size,
            MaxCroppingSize = size,
            CropGranularityX = 1,
            CropGranularityY = 1,
            CropAlignX = 1,
            CropAlignY = 1,
            MinOutputSize = size,
            MaxOutputSize = size,
            OutputGranularityX = 1,
            OutputGranularityY = 1,
            MinFrameInterval = format.FrameDuration,
            MaxFrameInterval = format.FrameDuration,
            MinBitsPerSecond = format.CapabilityBitsPerSecond,
            MaxBitsPerSecond = format.CapabilityBitsPerSecond,
        };
        Marshal.StructureToPtr(caps, capabilitiesPointer, false);
        return Constants.S_OK;
    }

    public HRESULT Set(in Guid propertySet, uint propertyId, nint instanceData, uint instanceSize, nint propertyData, uint propertySize) =>
        Constants.E_NOTIMPL;

    public HRESULT Get(in Guid propertySet, uint propertyId, nint instanceData, uint instanceSize, nint propertyData, uint propertySize, out uint returned)
    {
        returned = 0;
        if (propertySet != Constants.AMPROPSETID_Pin || propertyId != AmPropertyPinCategory)
            return Constants.E_NOTIMPL;
        if (propertyData == 0 || propertySize < Marshal.SizeOf<Guid>()) return Constants.E_POINTER;
        Marshal.StructureToPtr(Constants.PIN_CATEGORY_CAPTURE, propertyData, false);
        returned = checked((uint)Marshal.SizeOf<Guid>());
        return Constants.S_OK;
    }

    public HRESULT QuerySupported(in Guid propertySet, uint propertyId, out uint support)
    {
        support = propertySet == Constants.AMPROPSETID_Pin && propertyId == AmPropertyPinCategory
            ? KsPropertySupportGet
            : 0;
        return support == 0 ? Constants.E_NOTIMPL : Constants.S_OK;
    }

    internal HRESULT StartStreaming()
    {
        lock (_sync)
        {
            if (_streamThread is { IsAlive: true }) return Constants.S_OK;
            if (_allocator is null || _input is null) return Constants.S_OK;
            var result = _allocator.Object.Commit();
            if (result.IsError) return result;

            _stopSignal = new ManualResetEventSlim(false);
            _streamThread = new Thread(StreamLoop)
            {
                IsBackground = true,
                Name = "PocketCam DirectShow source",
            };
            _streamThread.Start();
            return Constants.S_OK;
        }
    }

    internal void StopStreaming()
    {
        Thread? thread;
        ManualResetEventSlim? signal;
        lock (_sync)
        {
            thread = _streamThread;
            signal = _stopSignal;
            signal?.Set();
        }
        if (thread is not null && thread != Thread.CurrentThread) thread.Join(TimeSpan.FromSeconds(5));
        lock (_sync)
        {
            _allocator?.Object.Decommit();
            _streamThread = null;
            _stopSignal?.Dispose();
            _stopSignal = null;
        }
    }

    private unsafe void StreamLoop()
    {
        var comResult = CoInitializeEx(0, 0); // COINIT_MULTITHREADED
        try
        {
            var started = Stopwatch.GetTimestamp();
            long frameIndex = 0;
            while (!(_stopSignal?.IsSet ?? true))
            {
                var allocator = _allocator;
                var input = _input;
                if (allocator is null || input is null) break;

                var result = allocator.Object.GetBuffer(out var rawSample, 0, 0, 0);
                if (result.IsError || rawSample is null) break;
                using (var sample = new ComObject<IMediaSample>(rawSample))
                {
                    result = sample.Object.GetPointer(out var destination);
                    if (result.IsError || destination == 0 || sample.Object.GetSize() < _format.BufferSize) break;

                    var pixels = _frameReader.ReadScaledBgra(_format.Width, _format.Height);
                    Marshal.Copy(pixels, 0, destination, pixels.Length);
                    sample.Object.SetActualDataLength(pixels.Length).ThrowOnError();
                    var sampleStart = frameIndex * _format.FrameDuration;
                    var sampleEnd = sampleStart + _format.FrameDuration;
                    sample.Object.SetTime((nint)(&sampleStart), (nint)(&sampleEnd)).ThrowOnError();
                    sample.Object.SetSyncPoint(true).ThrowOnError();
                    sample.Object.SetPreroll(_filter.State == FILTER_STATE.State_Paused).ThrowOnError();
                    sample.Object.SetDiscontinuity(frameIndex == 0).ThrowOnError();
                    result = input.Object.Receive(sample.Object);
                    if (result.IsError) break;
                }

                frameIndex++;
                var dueTicks = started + frameIndex * Stopwatch.Frequency / _format.FramesPerSecond;
                var remainingTicks = dueTicks - Stopwatch.GetTimestamp();
                if (remainingTicks > 0)
                {
                    var delay = TimeSpan.FromSeconds(remainingTicks / (double)Stopwatch.Frequency);
                    _stopSignal?.Wait(delay);
                }
            }
        }
        catch (Exception error)
        {
            ComHosting.Trace(error.ToString());
        }
        finally
        {
            if (comResult >= 0) CoUninitialize();
        }
    }

    private void ReleaseConnection()
    {
        _allocator.SafeDispose();
        _input.SafeDispose();
        _connected.SafeDispose();
        _allocator = null;
        _input = null;
        _connected = null;
    }

    private static IComObject<T>? Retain<T>(object value) where T : class
    {
        var pointer = DirectN.Extensions.Com.ComObject.WithComInstance(
            value,
            unknown => DirectN.Extensions.Com.ComObject.QueryInterface<T>(unknown, false));
        return pointer == 0 ? null : DirectN.Extensions.Com.ComObject.FromPointer<T>(pointer);
    }

    private static void CopyName(ref InlineArraySystemChar_128 destination, string value)
    {
        var length = Math.Min(127, value.Length);
        for (var index = 0; index < length; index++) destination[index] = value[index];
        destination[length] = '\0';
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint coInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    public void Dispose()
    {
        if (_disposed) return;
        StopStreaming();
        lock (_sync) ReleaseConnection();
        _frameReader.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
