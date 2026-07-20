namespace VCamNetSampleSourceAOT.DirectShow;

[Guid(Shared.CLSID_DirectShowCamera)]
[ProgId("PocketCam.DirectShowCamera")]
[GeneratedComClass]
public sealed partial class DirectShowCameraFilter : IBaseFilter, IAMFilterMiscFlags, IDisposable
{
    private readonly object _sync = new();
    private readonly DirectShowCameraPin _pin;
    private IComObject<IFilterGraph>? _graph;
    private IComObject<IReferenceClock>? _clock;
    private volatile FILTER_STATE _state = FILTER_STATE.State_Stopped;
    private string _name = "PocketCam Virtual Camera";
    private bool _disposed;

    public DirectShowCameraFilter()
    {
        _pin = new DirectShowCameraPin(this);
    }

    internal FILTER_STATE State => _state;

    public HRESULT GetClassID(out Guid classId)
    {
        classId = typeof(DirectShowCameraFilter).GUID;
        return Constants.S_OK;
    }

    public HRESULT Stop()
    {
        _state = FILTER_STATE.State_Stopped;
        _pin.StopStreaming();
        return Constants.S_OK;
    }

    public HRESULT Pause()
    {
        var result = _pin.StartStreaming();
        if (result.IsError) return result;
        _state = FILTER_STATE.State_Paused;
        return Constants.S_OK;
    }

    public HRESULT Run(long startTime)
    {
        _pin.ForwardNewSegment();
        var result = _pin.StartStreaming();
        if (result.IsError) return result;
        _state = FILTER_STATE.State_Running;
        return Constants.S_OK;
    }

    public HRESULT GetState(uint timeoutMilliseconds, out FILTER_STATE state)
    {
        state = _state;
        return Constants.S_OK;
    }

    public HRESULT SetSyncSource(IReferenceClock? clock)
    {
        lock (_sync)
        {
            _clock.SafeDispose();
            _clock = clock is null ? null : Retain<IReferenceClock>(clock);
            return Constants.S_OK;
        }
    }

    public HRESULT GetSyncSource(out IReferenceClock clock)
    {
        lock (_sync)
        {
            clock = _clock?.Object!;
            return Constants.S_OK;
        }
    }

    public HRESULT EnumPins(out IEnumPins enumerator)
    {
        enumerator = new DirectShowPinEnumerator(_pin);
        return Constants.S_OK;
    }

    public HRESULT FindPin(PWSTR id, out IPin pin)
    {
        if (string.Equals(id.ToString(), DirectShowCameraPin.Id, StringComparison.OrdinalIgnoreCase))
        {
            pin = _pin;
            return Constants.S_OK;
        }
        pin = null!;
        return DirectShowHResults.NotFound;
    }

    public HRESULT QueryFilterInfo(out FILTER_INFO info)
    {
        lock (_sync)
        {
            info = default;
            CopyName(ref info.achName, _name);
            if (_graph is not null)
            {
                info.pGraph = DirectN.Extensions.Com.ComObject.WithComInstance(_graph, pointer =>
                {
                    Marshal.AddRef(pointer);
                    return pointer;
                });
            }
            return Constants.S_OK;
        }
    }

    public HRESULT JoinFilterGraph(IFilterGraph? graph, PWSTR name)
    {
        lock (_sync)
        {
            _graph.SafeDispose();
            _graph = graph is null ? null : Retain<IFilterGraph>(graph);
            var requestedName = name.ToString();
            _name = string.IsNullOrWhiteSpace(requestedName) ? "PocketCam Virtual Camera" : requestedName;
            return Constants.S_OK;
        }
    }

    public HRESULT QueryVendorInfo(out PWSTR vendorInfo)
    {
        vendorInfo = PWSTR.Null;
        return Constants.E_NOTIMPL;
    }

    public uint GetMiscFlags() => 1; // AM_FILTER_MISC_FLAGS_IS_SOURCE

    private static IComObject<T>? Retain<T>(T value) where T : class
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

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _pin.Dispose();
        _clock.SafeDispose();
        _graph.SafeDispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
