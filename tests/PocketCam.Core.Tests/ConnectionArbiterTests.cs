using PocketCam.Core.Connections;

namespace PocketCam.Core.Tests;

public sealed class ConnectionArbiterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SelectsUsbOverWifiAndBluetooth()
    {
        var arbiter = new ConnectionArbiter();
        var candidates = new[]
        {
            Healthy("bt", TransportKind.Bluetooth),
            Healthy("wifi", TransportKind.WiFi),
            Healthy("usb", TransportKind.Usb),
        };

        var result = arbiter.Evaluate(candidates, Now);

        Assert.Equal("usb", result.ActiveId);
        Assert.True(result.Changed);
    }

    [Fact]
    public void FailedUsbFallsBackToAlreadyHealthyWifiImmediately()
    {
        var arbiter = new ConnectionArbiter();
        arbiter.Evaluate([Healthy("usb", TransportKind.Usb), Healthy("wifi", TransportKind.WiFi)], Now);
        var disconnectedUsb = Healthy("usb", TransportKind.Usb) with { IsConnected = false };

        var result = arbiter.Evaluate([disconnectedUsb, Healthy("wifi", TransportKind.WiFi)], Now.AddMilliseconds(10));

        Assert.Equal("wifi", result.ActiveId);
        Assert.True(result.Changed);
    }

    [Fact]
    public void BetterConnectionMustRemainStableBeforePromotion()
    {
        var arbiter = new ConnectionArbiter();
        arbiter.Evaluate([Healthy("wifi", TransportKind.WiFi)], Now);
        var both = new[] { Healthy("wifi", TransportKind.WiFi), Healthy("usb", TransportKind.Usb) };

        Assert.False(arbiter.Evaluate(both, Now.AddMilliseconds(100)).Changed);
        Assert.False(arbiter.Evaluate(both, Now.AddMilliseconds(700)).Changed);
        Assert.True(arbiter.Evaluate(both, Now.AddMilliseconds(900)).Changed);
        Assert.Equal("usb", arbiter.ActiveId);
    }

    [Fact]
    public void StaleFramesMakeConnectionUnhealthy()
    {
        var arbiter = new ConnectionArbiter();
        var stale = Healthy("usb", TransportKind.Usb) with { LastFrameAt = Now.Subtract(TimeSpan.FromSeconds(4)) };

        var result = arbiter.Evaluate([stale, Healthy("wifi", TransportKind.WiFi)], Now);

        Assert.Equal("wifi", result.ActiveId);
    }

    private static TransportSnapshot Healthy(string id, TransportKind kind) =>
        new(id, "phone-1", kind, true, Now.Subtract(TimeSpan.FromSeconds(10)), Now, 20);
}

