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
    private readonly IEndpointDiscovery[] _discoveries =
    [
        new WifiDiscoveryService(),
        new UsbDiscoveryService(),
        new BluetoothDiscoveryService(),
    ];
    private readonly List<Task> _backgroundTasks = [];

    public event Action<VideoFrame, TransportSession>? ActiveFrameReceived;
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

    public async Task ApplySettingsAsync(CameraSettings settings, CancellationToken cancellationToken = default)
    {
        if (_arbiter.ActiveId is not null && _sessions.TryGetValue(_arbiter.ActiveId, out var active))
        {
            await active.SendSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
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

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PublishState();
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    private SelectionResult Evaluate() => _arbiter.Evaluate(
        _sessions.Values.Select(session => session.Snapshot).ToArray(),
        DateTimeOffset.UtcNow);

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
