namespace VCamNetSampleSourceAOT.DirectShow;

internal static partial class DirectShowRegistration
{
    private const uint MeritDoNotUse = 0x00200000;
    private const uint RegPinFlagOutput = 0x00000002;

    public static unsafe HRESULT Register()
    {
        var comResult = CoInitializeEx(0, 2); // COINIT_APARTMENTTHREADED
        try
        {
            using var mapper = DirectN.Extensions.Com.ComObject.CoCreate<IFilterMapper2>(Constants.CLSID_FilterMapper2);
            if (mapper is null) return Constants.E_FAIL;

            var majorType = Constants.MEDIATYPE_Video;
            var subtype = Constants.MEDIASUBTYPE_RGB32;
            var pinCategory = Constants.PIN_CATEGORY_CAPTURE;
            var pinType = new REGPINTYPES
            {
                clsMajorType = (nint)(&majorType),
                clsMinorType = (nint)(&subtype),
            };
            var pin = new REGFILTERPINS2
            {
                dwFlags = RegPinFlagOutput,
                cInstances = 1,
                nMediaTypes = 1,
                lpMediaType = (nint)(&pinType),
                clsPinCategory = (nint)(&pinCategory),
            };
            var registration = new REGFILTER2
            {
                dwVersion = 2,
                dwMerit = MeritDoNotUse,
            };
            registration.Anonymous.Anonymous2.cPins2 = 1;
            registration.Anonymous.Anonymous2.rgPins2 = (nint)(&pin);

            var classId = typeof(DirectShowCameraFilter).GUID;
            var category = Constants.CLSID_VideoInputDeviceCategory;
            var friendlyName = PWSTR.From("PocketCam Virtual Camera");
            try
            {
                return mapper.Object.RegisterFilter(
                    classId,
                    friendlyName,
                    0,
                    category,
                    PWSTR.Null,
                    registration);
            }
            finally
            {
                PWSTR.Dispose(ref friendlyName);
            }
        }
        finally
        {
            if (comResult >= 0) CoUninitialize();
        }
    }

    public static HRESULT Unregister()
    {
        var comResult = CoInitializeEx(0, 2);
        try
        {
            using var mapper = DirectN.Extensions.Com.ComObject.CoCreate<IFilterMapper2>(Constants.CLSID_FilterMapper2);
            if (mapper is null) return Constants.S_OK;
            var classId = typeof(DirectShowCameraFilter).GUID;
            var category = Constants.CLSID_VideoInputDeviceCategory;
            // Unregistration is intentionally idempotent.
            mapper.Object.UnregisterFilter(category, PWSTR.Null, classId);
            return Constants.S_OK;
        }
        catch
        {
            return Constants.S_OK;
        }
        finally
        {
            if (comResult >= 0) CoUninitialize();
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint coInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();
}
