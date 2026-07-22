using System.Collections.Concurrent;
using System.Threading.Channels;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Connections;

public sealed record ConnectionState(
    string Id,
    string DeviceId,
    string DeviceName,
    TransportKind Kind,
    bool Connected,
    bool Active,
    double LatencyMilliseconds);

public sealed class ConnectionManager : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Channel<TransportEndpoint> _endpoints = Channel.CreateUnbounded<TransportEndpoint>();
    private readonly ConcurrentDictionary<string, TransportSession> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _connecting = new(StringComparer.Ordinal);
    private readonly ConnectionArbiter _arbiter = new();
    private readonly object _arbiterGate = new();
    private readonly IEndpointDiscovery[] _discoveries =
    [
        new WifiDiscoveryService(),
        new LanProbeDiscoveryService(),
        new UsbDiscoveryService(),
        new BluetoothDiscoveryService(),
    ];
    private readonly List<Task> _backgroundTasks = [];
    private readonly object _settingsGate = new();
    private readonly object _metricsGate = new();
    private string? _lastSettingsDeviceId;
    private CameraSettings? _lastSettings;
    private string? _lastMetricsEndpointId;
    private StreamingMetrics? _lastMetrics;

    public event Action<VideoFrame, TransportSession>? ActiveFrameReceived;
    public event Action<CameraSettings>? SettingsChanged;
    public event Action<StreamingMetrics>? MetricsChanged;
    public event Action<IReadOnlyList<ConnectionState>, SelectionResult>? StateChanged;

    public Task StartAsync()
    {
        foreach (var discovery in _discoveries)
        {
            _backgroundTasks.Add(RunDiscoveryAsync(discovery, _cancellation.Token));
        }
        _backgroundTasks.Add(ProcessEndpointsAsync(_cancellation.Token));
        _backgroundTasks.Add(MonitorAsync(_cancellation.Token));
        return Task.CompletedTask;
    }

    public async Task<CameraSettings> ApplySettingsAsync(CameraSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        if (_arbiter.ActiveId is null || !_sessions.TryGetValue(_arbiter.ActiveId, out var active))
        {
            throw new InvalidOperationException("Nenhum celular está conectado para receber as configurações.");
        }

        var confirmation = new TaskCompletionSource<CameraSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Confirm(TransportSession session, CameraSettings applied)
        {
            if (ReferenceEquals(session, active) && applied == settings) confirmation.TrySetResult(applied);
        }

        active.SettingsReceived += Confirm;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellation.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await active.SendSettingsAsync(settings, timeout.Token).ConfigureAwait(false);
            return await confirmation.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            active.SettingsReceived -= Confirm;
        }
    }

    private async Task RunDiscoveryAsync(IEndpointDiscovery discovery, CancellationToken cancellationToken)
    {
        try
        {
            await discovery.DiscoverAsync(_endpoints.Writer, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch
        {
            // Other discovery mechanisms remain active; monitor continues to report current state.
        }
    }

    private async Task ProcessEndpointsAsync(CancellationToken cancellationToken)
    {
        await foreach (var endpoint in _endpoints.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_sessions.TryGetValue(endpoint.Id, out var existing) && existing.IsConnected) continue;
            if (!_connecting.TryAdd(endpoint.Id, 0)) continue;
            _ = ConnectEndpointAsync(endpoint, cancellationToken);
        }
    }

    private async Task ConnectEndpointAsync(TransportEndpoint endpoint, CancellationToken cancellationToken)
    {
        var session = new TransportSession(endpoint);
        session.FrameReceived += OnFrameReceived;
        session.SettingsReceived += OnSettingsReceived;
        session.MetricsReceived += OnMetricsReceived;
        session.StateChanged += OnSessionStateChanged;
        try
        {
            await session.StartAsync(cancellationToken).ConfigureAwait(false);
            if (!session.IsConnected)
            {
                await session.DisposeAsync().ConfigureAwait(false);
                return;
            }
            if (_sessions.TryGetValue(endpoint.Id, out var old)) await old.DisposeAsync().ConfigureAwait(false);
            _sessions[endpoint.Id] = session;
            PublishState();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _connecting.TryRemove(endpoint.Id, out _);
        }
    }

    private void OnFrameReceived(TransportSession session, VideoFrame frame)
    {
        var selection = Evaluate();
        if (selection.ActiveId == session.Endpoint.Id)
        {
            ActiveFrameReceived?.Invoke(frame, session);
        }
    }

    private void OnSessionStateChanged(TransportSession session)
    {
        if (!session.IsConnected && _sessions.TryRemove(session.Endpoint.Id, out _))
        {
            _ = Task.Run(async () => await session.DisposeAsync().ConfigureAwait(false));
        }
        PublishState();
    }

    private void OnSettingsReceived(TransportSession session, CameraSettings settings)
    {
        var selection = Evaluate();
        if (selection.ActiveId == session.Endpoint.Id) PublishActiveSettings(selection);
    }

    private void OnMetricsReceived(TransportSession session, StreamingMetrics metrics)
    {
        var selection = Evaluate();
        if (selection.ActiveId == session.Endpoint.Id) PublishActiveMetrics(selection);
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PublishState();
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    private SelectionResult Evaluate()
    {
        var snapshots = _sessions.Values.Select(session => session.Snapshot).ToArray();
        lock (_arbiterGate)
        {
            return _arbiter.Evaluate(snapshots, DateTimeOffset.UtcNow);
        }
    }

    private void PublishState()
    {
        var selection = Evaluate();
        var states = _sessions.Values
            .Select(session => new ConnectionState(
                session.Endpoint.Id,
                session.DeviceId,
                session.DeviceName,
                session.Endpoint.Kind,
                session.IsConnected,
                session.Endpoint.Id == selection.ActiveId,
                session.RoundTripMilliseconds))
            .OrderByDescending(state => state.Active)
            .ThenByDescending(state => state.Kind)
            .ToArray();
        StateChanged?.Invoke(states, selection);
        UpdateStreamingRoutes(selection);
        PublishActiveSettings(selection);
        PublishActiveMetrics(selection);
    }

    private void UpdateStreamingRoutes(SelectionResult selection)
    {
        foreach (var session in _sessions.Values.Where(item => item.IsConnected))
        {
            var enabled = session.Endpoint.Id == selection.ActiveId;
            _ = ApplyStreamingRouteAsync(session, enabled);
        }
    }

    private async Task ApplyStreamingRouteAsync(TransportSession session, bool enabled)
    {
        try
        {
            await session.SetStreamingEnabledAsync(enabled, _cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch
        {
            // The monitor retries a failed control write or replaces a dead session.
        }
    }

    private void PublishActiveSettings(SelectionResult selection)
    {
        if (selection.ActiveId is null ||
            !_sessions.TryGetValue(selection.ActiveId, out var active) ||
            active.CurrentSettings is not { } settings)
        {
            return;
        }

        lock (_settingsGate)
        {
            if (_lastSettingsDeviceId == active.DeviceId && _lastSettings == settings) return;
            _lastSettingsDeviceId = active.DeviceId;
            _lastSettings = settings;
        }
        SettingsChanged?.Invoke(settings);
    }

    private void PublishActiveMetrics(SelectionResult selection)
    {
        if (selection.ActiveId is null ||
            !_sessions.TryGetValue(selection.ActiveId, out var active) ||
            active.CurrentMetrics is not { } metrics)
        {
            return;
        }

        lock (_metricsGate)
        {
            if (_lastMetricsEndpointId == active.Endpoint.Id && _lastMetrics == metrics) return;
            _lastMetricsEndpointId = active.Endpoint.Id;
            _lastMetrics = metrics;
        }
        MetricsChanged?.Invoke(metrics);
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _endpoints.Writer.TryComplete();
        foreach (var session in _sessions.Values) await session.DisposeAsync().ConfigureAwait(false);
        try { await Task.WhenAll(_backgroundTasks).ConfigureAwait(false); } catch (OperationCanceledException) { }
        _cancellation.Dispose();
    }
}
