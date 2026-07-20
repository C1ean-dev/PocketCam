using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DirectN;

namespace PocketCam.VirtualCamera.Host;

internal static class DirectShowRegistrar
{
    private const uint MeritDoNotUse = 0x00200000;
    private const uint RegPinFlagOutput = 0x00000002;
    private static readonly Guid FilterMapperClassId = new("CDA42200-BD88-11D0-BD4E-00A0C911CE86");
    private static readonly Guid VideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");
    private static readonly Guid VideoMediaType = new("73646976-0000-0010-8000-00AA00389B71");
    private static readonly Guid Rgb32MediaSubtype = new("E436EB7E-524F-11CE-9F53-0020AF0BA770");
    private static readonly Guid CapturePinCategory = new("FB6C4281-0353-11D1-905F-0000C0CC16BA");
    private static readonly Guid FilterClassId = new("4273C9FA-4D74-4C61-9B5B-7D032BFCBDA8");

    public static int Register()
    {
        var mapper = CreateMapper();
        IMoniker? moniker = null;
        var allocations = new List<nint>();
        try
        {
            mapper.CreateCategory(VideoInputDeviceCategory, MeritDoNotUse, "Video Capture Sources").ThrowOnError();

            var majorType = Allocate(VideoMediaType, allocations);
            var subtype = Allocate(Rgb32MediaSubtype, allocations);
            var pinCategory = Allocate(CapturePinCategory, allocations);
            var pinType = Allocate(new REGPINTYPES
            {
                clsMajorType = majorType,
                clsMinorType = subtype,
            }, allocations);
            var pin = Allocate(new REGFILTERPINS2
            {
                dwFlags = RegPinFlagOutput,
                cInstances = 1,
                nMediaTypes = 1,
                lpMediaType = pinType,
                clsPinCategory = pinCategory,
            }, allocations);

            var unionBytes = new byte[16];
            BitConverter.GetBytes(1u).CopyTo(unionBytes, 0);
            BitConverter.GetBytes(pin.ToInt64()).CopyTo(unionBytes, 8);
            var registration = new REGFILTER2
            {
                dwVersion = 2,
                dwMerit = MeritDoNotUse,
                __union_2 = new REGFILTER2__union_0 { __bits = unionBytes },
            };

            mapper.RegisterFilter(
                FilterClassId,
                "PocketCam Virtual Camera",
                out moniker,
                VideoInputDeviceCategory,
                FilterClassId.ToString("B"),
                ref registration).ThrowOnError();
            return 0;
        }
        finally
        {
            if (moniker is not null) Marshal.ReleaseComObject(moniker);
            foreach (var allocation in allocations) Marshal.FreeCoTaskMem(allocation);
            Marshal.ReleaseComObject(mapper);
        }
    }

    public static int Unregister()
    {
        var mapper = CreateMapper();
        try
        {
            mapper.UnregisterFilter(VideoInputDeviceCategory, FilterClassId.ToString("B"), FilterClassId);
            return 0;
        }
        finally
        {
            Marshal.ReleaseComObject(mapper);
        }
    }

    private static IFilterMapper2 CreateMapper()
    {
        var type = Type.GetTypeFromCLSID(FilterMapperClassId, throwOnError: true)!;
        return (IFilterMapper2)System.Activator.CreateInstance(type)!;
    }

    private static nint Allocate<T>(T value, ICollection<nint> allocations) where T : struct
    {
        var pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(value, pointer, false);
        allocations.Add(pointer);
        return pointer;
    }
}
