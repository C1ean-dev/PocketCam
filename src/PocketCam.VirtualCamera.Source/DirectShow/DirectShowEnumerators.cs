namespace VCamNetSampleSourceAOT.DirectShow;

[GeneratedComClass]
internal sealed partial class DirectShowPinEnumerator : IEnumPins
{
    private readonly DirectShowCameraPin _pin;
    private uint _position;

    public DirectShowPinEnumerator(DirectShowCameraPin pin, uint position = 0)
    {
        _pin = pin;
        _position = position;
    }

    public HRESULT Next(uint count, nint[] pins, nint fetchedPointer)
    {
        if (pins is null || pins.Length < count || count != 1 && fetchedPointer == 0)
            return Constants.E_POINTER;

        uint fetched = 0;
        if (_position == 0 && count > 0)
        {
            pins[0] = DirectN.Extensions.Com.ComObject.GetOrCreateComInstance<IPin>(_pin);
            _position = 1;
            fetched = 1;
        }
        if (fetchedPointer != 0) Marshal.WriteInt32(fetchedPointer, checked((int)fetched));
        return fetched == count ? Constants.S_OK : Constants.S_FALSE;
    }

    public HRESULT Skip(uint count)
    {
        var remaining = _position == 0 ? 1u : 0u;
        _position = Math.Min(1u, _position + count);
        return count <= remaining ? Constants.S_OK : Constants.S_FALSE;
    }

    public HRESULT Reset()
    {
        _position = 0;
        return Constants.S_OK;
    }

    public HRESULT Clone(out IEnumPins enumerator)
    {
        enumerator = new DirectShowPinEnumerator(_pin, _position);
        return Constants.S_OK;
    }
}

[GeneratedComClass]
internal sealed partial class DirectShowMediaTypeEnumerator : IEnumMediaTypes
{
    private uint _position;

    public DirectShowMediaTypeEnumerator(uint position = 0)
    {
        _position = position;
    }

    public HRESULT Next(uint count, AM_MEDIA_TYPE[] mediaTypes, nint fetchedPointer)
    {
        if (mediaTypes is null || mediaTypes.Length < count || count != 1 && fetchedPointer == 0)
            return Constants.E_POINTER;

        uint fetched = 0;
        while (fetched < count && _position < DirectShowMediaType.Formats.Length)
        {
            mediaTypes[fetched] = DirectShowMediaType.Create(DirectShowMediaType.Formats[_position]);
            fetched++;
            _position++;
        }
        if (fetchedPointer != 0) Marshal.WriteInt32(fetchedPointer, checked((int)fetched));
        return fetched == count ? Constants.S_OK : Constants.S_FALSE;
    }

    public HRESULT Skip(uint count)
    {
        var remaining = checked((uint)DirectShowMediaType.Formats.Length) - _position;
        _position = Math.Min(checked((uint)DirectShowMediaType.Formats.Length), _position + count);
        return count <= remaining ? Constants.S_OK : Constants.S_FALSE;
    }

    public HRESULT Reset()
    {
        _position = 0;
        return Constants.S_OK;
    }

    public HRESULT Clone(out IEnumMediaTypes enumerator)
    {
        enumerator = new DirectShowMediaTypeEnumerator(_position);
        return Constants.S_OK;
    }
}
