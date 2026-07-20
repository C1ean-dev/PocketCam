using System.Windows.Media;
using PocketCam.Core.Connections;
using PocketCam.Desktop.Connections;

namespace PocketCam.Desktop.ViewModels;

public sealed class ConnectionItemViewModel(ConnectionState state)
{
    public string DeviceName { get; } = state.DeviceName;
    public string TransportLabel { get; } = state.Kind switch
    {
        TransportKind.Usb => $"USB · {state.LatencyMilliseconds:F0} ms",
        TransportKind.WiFi => $"Wi-Fi · {state.LatencyMilliseconds:F0} ms",
        TransportKind.Bluetooth => $"Bluetooth · {state.LatencyMilliseconds:F0} ms",
        _ => state.Kind.ToString(),
    };
    public string StateLabel { get; } = state.Active ? "ATIVA" : state.Connected ? "RESERVA" : "OFFLINE";
    public Brush StateBrush { get; } = new SolidColorBrush(
        state.Active ? Color.FromRgb(0x00, 0xD4, 0xA6)
        : state.Connected ? Color.FromRgb(0xA8, 0xC7, 0xC8)
        : Color.FromRgb(0xFF, 0x93, 0x8A));
}

