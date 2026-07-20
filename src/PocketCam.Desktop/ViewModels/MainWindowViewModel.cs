using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;
using PocketCam.Desktop.Connections;
using PocketCam.Desktop.Video;
using PocketCam.Desktop.VirtualCamera;

namespace PocketCam.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ConnectionManager _connectionManager = new();
    private readonly SharedMemoryFrameSink _frameSink = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();
    private BitmapSource? _previewFrame;
    private string _statusText = "Procurando celulares PocketCam…";
    private string _activeTransportLabel = "DESCOBERTO AUTOMATICAMENTE";
    private string _latencyText = "— ms";
    private double _framesPerSecond;
    private long _framesSinceSample;
    private string _selectedResolution = "1280 × 720";
    private int _selectedFps = 20;
    private int _jpegQuality = 80;
    private string _selectedLens = "Traseira";
    private bool _disposed;

    public MainWindowViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _connectionManager.ActiveFrameReceived += OnFrameReceived;
        _connectionManager.StateChanged += OnStateChanged;
        ApplySettingsCommand = new AsyncCommand(ApplySettingsAsync);
        InstallVirtualCameraCommand = new AsyncCommand(InstallVirtualCameraAsync);
        VirtualCameraInstaller.Start();
    }

    public ObservableCollection<ConnectionItemViewModel> Connections { get; } = [];
    public IReadOnlyList<string> ResolutionOptions { get; } = ["640 × 480", "1280 × 720", "1920 × 1080"];
    public IReadOnlyList<string> LensOptions { get; } = ["Traseira", "Frontal"];
    public AsyncCommand ApplySettingsCommand { get; }
    public AsyncCommand InstallVirtualCameraCommand { get; }
    public string VirtualCameraText { get; private set; } = VirtualCameraInstaller.StatusText;

    public BitmapSource? PreviewFrame
    {
        get => _previewFrame;
        private set
        {
            if (SetProperty(ref _previewFrame, value)) OnPropertyChanged(nameof(EmptyPreviewVisibility));
        }
    }

    public Visibility EmptyPreviewVisibility => PreviewFrame is null ? Visibility.Visible : Visibility.Collapsed;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string ActiveTransportLabel { get => _activeTransportLabel; private set => SetProperty(ref _activeTransportLabel, value); }
    public string LatencyText { get => _latencyText; private set => SetProperty(ref _latencyText, value); }
    public double FramesPerSecond { get => _framesPerSecond; private set => SetProperty(ref _framesPerSecond, value); }
    public string SelectedResolution { get => _selectedResolution; set => SetProperty(ref _selectedResolution, value); }
    public int SelectedFps { get => _selectedFps; set => SetProperty(ref _selectedFps, value); }
    public int JpegQuality { get => _jpegQuality; set => SetProperty(ref _jpegQuality, value); }
    public string SelectedLens { get => _selectedLens; set => SetProperty(ref _selectedLens, value); }

    public Task StartAsync() => _connectionManager.StartAsync();

    private void OnFrameReceived(VideoFrame frame, TransportSession session)
    {
        if (!_renderGate.Wait(0)) return;
        _ = Task.Run(() =>
        {
            try
            {
                var bitmap = FrameRenderer.Decode(frame);
                var bgra = FrameRenderer.ToBgra32(bitmap);
                _frameSink.Publish(bgra, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000);
                Interlocked.Increment(ref _framesSinceSample);
                _dispatcher.BeginInvoke(() => PreviewFrame = bitmap, DispatcherPriority.Render);
            }
            catch
            {
                // A damaged JPEG affects only one frame.
            }
            finally
            {
                _renderGate.Release();
            }
        });
    }

    private void OnStateChanged(IReadOnlyList<ConnectionState> states, SelectionResult selection)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Connections.Clear();
            foreach (var state in states) Connections.Add(new ConnectionItemViewModel(state));
            var active = states.FirstOrDefault(item => item.Active);
            if (active is null)
            {
                StatusText = "Procurando celulares PocketCam…";
                ActiveTransportLabel = "DESCOBERTO AUTOMATICAMENTE";
                LatencyText = "— ms";
            }
            else
            {
                StatusText = $"{active.DeviceName} conectado · {selection.Reason}";
                ActiveTransportLabel = active.Kind switch
                {
                    TransportKind.Usb => "USB · MELHOR ROTA",
                    TransportKind.WiFi => "WI-FI · ATIVA",
                    TransportKind.Bluetooth => "BLUETOOTH · CONTINGÊNCIA",
                    _ => active.Kind.ToString().ToUpperInvariant(),
                };
                LatencyText = $"{active.LatencyMilliseconds:F0} ms";
            }

            if (_fpsClock.Elapsed >= TimeSpan.FromSeconds(1))
            {
                FramesPerSecond = Interlocked.Exchange(ref _framesSinceSample, 0) / _fpsClock.Elapsed.TotalSeconds;
                _fpsClock.Restart();
            }
        });
    }

    private async Task ApplySettingsAsync()
    {
        var parts = SelectedResolution.Split('×', StringSplitOptions.TrimEntries);
        var settings = new CameraSettings(
            int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            SelectedFps,
            JpegQuality,
            SelectedLens == "Frontal" ? "front" : "back");
        await _connectionManager.ApplySettingsAsync(settings).ConfigureAwait(true);
        StatusText = "Configurações enviadas ao celular.";
    }

    private async Task InstallVirtualCameraAsync()
    {
        try
        {
            await VirtualCameraInstaller.InstallAsync().ConfigureAwait(true);
            VirtualCameraText = "Câmera virtual instalada e pronta para uso.";
        }
        catch (Exception error)
        {
            VirtualCameraText = error.Message;
        }
        OnPropertyChanged(nameof(VirtualCameraText));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connectionManager.ActiveFrameReceived -= OnFrameReceived;
        _connectionManager.StateChanged -= OnStateChanged;
        _connectionManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _frameSink.Dispose();
        VirtualCameraInstaller.Stop();
        _renderGate.Dispose();
    }
}
