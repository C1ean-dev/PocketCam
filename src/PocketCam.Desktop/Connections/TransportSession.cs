using System.Buffers.Binary;
using System.Diagnostics;
using PocketCam.Core.Connections;
using PocketCam.Core.Protocol;

namespace PocketCam.Desktop.Connections;

public sealed class TransportSession : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Stream? _stream;
    private Task? _readerTask;
    private Task? _pingTask;
    private int _sequence;
    private long _lastActivityTicks;
    private long _roundTripBits;
    private int _failures;
    private int _requestedStreaming = -1;

    public TransportSession(TransportEndpoint endpoint)
    {
        Endpoint = endpoint;
        ConnectedAt = DateTimeOffset.UtcNow;
        _lastActivityTicks = ConnectedAt.UtcTicks;
    }

    public TransportEndpoint Endpoint { get; }
    public DateTimeOffset ConnectedAt { get; private set; }
    public HelloMessage? Hello { get; private set; }
    public CameraSettings? CurrentSettings { get; private set; }
    public bool IsConnected { get; private set; }
    public string DeviceId => Hello?.DeviceId ?? Endpoint.ExpectedDeviceId ?? Endpoint.Id;
    public string DeviceName => Hello?.DeviceName ?? Endpoint.DeviceName;
    public double RoundTripMilliseconds => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _roundTripBits));

    public event Action<TransportSession, VideoFrame>? FrameReceived;
    public event Action<TransportSession, CameraSettings>? SettingsReceived;
    public event Action<TransportSession>? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, cancellationToken);
        _stream = await TransportConnector.ConnectAsync(Endpoint, linked.Token).ConfigureAwait(false);
        ConnectedAt = DateTimeOffset.UtcNow;
        IsConnected = true;
        StateChanged?.Invoke(this);
        _readerTask = ReadLoopAsync(_cancellation.Token);
        _pingTask = PingLoopAsync(_cancellation.Token);
    }

    public TransportSnapshot Snapshot => new(
        Endpoint.Id,
        DeviceId,
        Endpoint.Kind,
        IsConnected,
        ConnectedAt,
        new DateTimeOffset(Interlocked.Read(ref _lastActivityTicks), TimeSpan.Zero),
        RoundTripMilliseconds,
        Volatile.Read(ref _failures));

    public Task SendSettingsAsync(CameraSettings settings, CancellationToken cancellationToken = default) =>
        SendAsync(MessageType.Settings, JsonPayload.Serialize(settings.Validate()), cancellationToken);

    public async Task SetStreamingEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var requested = enabled ? 1 : 0;
        if (Interlocked.Exchange(ref _requestedStreaming, requested) == requested) return;
        try
        {
            await SendAsync(
                MessageType.Status,
                JsonPayload.Serialize(new StreamControl(enabled)),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.CompareExchange(ref _requestedStreaming, -1, requested);
            throw;
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await ProtocolCodec.ReadAsync(_stream!, cancellationToken).ConfigureAwait(false);
                Interlocked.Exchange(ref _lastActivityTicks, DateTimeOffset.UtcNow.UtcTicks);
                switch (message.Type)
                {
                    case MessageType.Hello:
                        Hello = JsonPayload.Deserialize<HelloMessage>(message.Payload);
                        Interlocked.Exchange(ref _failures, 0);
                        StateChanged?.Invoke(this);
                        break;
                    case MessageType.Frame:
                        Interlocked.Exchange(ref _failures, 0);
                        FrameReceived?.Invoke(this, VideoFrame.FromPayload(message.Payload));
                        break;
                    case MessageType.Settings:
                        var settings = JsonPayload.Deserialize<CameraSettings>(message.Payload).Validate();
                        CurrentSettings = settings;
                        SettingsReceived?.Invoke(this, settings);
                        break;
                    case MessageType.Pong:
                        HandlePong(message.Payload);
                        break;
                    case MessageType.Error:
                        Interlocked.Increment(ref _failures);
                        StateChanged?.Invoke(this);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception)
        {
            Interlocked.Exchange(ref _failures, 3);
        }
        finally
        {
            IsConnected = false;
            StateChanged?.Invoke(this);
        }
    }

    private async Task PingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var payload = new byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(payload, _clock.ElapsedTicks);
                await SendAsync(MessageType.Ping, payload, cancellationToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception)
        {
            Interlocked.Increment(ref _failures);
        }
    }

    private void HandlePong(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 8) return;
        var sentTicks = BinaryPrimitives.ReadInt64LittleEndian(payload);
        var milliseconds = (_clock.ElapsedTicks - sentTicks) * 1_000d / Stopwatch.Frequency;
        Interlocked.Exchange(ref _roundTripBits, BitConverter.DoubleToInt64Bits(Math.Max(0, milliseconds)));
        StateChanged?.Invoke(this);
    }

    private async Task SendAsync(MessageType type, byte[] payload, CancellationToken cancellationToken)
    {
        if (_stream is null) throw new InvalidOperationException("Transport has not connected.");
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ProtocolCodec.WriteAsync(
                _stream,
                ProtocolMessage.Create(type, unchecked((uint)Interlocked.Increment(ref _sequence)), payload),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _stream?.Dispose();
        if (_readerTask is not null)
        {
            try { await _readerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        if (_pingTask is not null)
        {
            try { await _pingTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        _writeLock.Dispose();
        _cancellation.Dispose();
    }
}
