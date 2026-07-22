namespace PocketCam.Core.Connections;

public sealed record TransportSnapshot(
    string Id,
    string DeviceId,
    TransportKind Kind,
    bool IsConnected,
    DateTimeOffset ConnectedAt,
    DateTimeOffset LastActivityAt,
    double RoundTripMilliseconds,
    int ConsecutiveFailures = 0);

public sealed record SelectionResult(string? PreviousId, string? ActiveId, bool Changed, string Reason);

public sealed class ConnectionArbiter
{
    public static readonly TimeSpan ActivityTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan PromotionDelay = TimeSpan.FromMilliseconds(750);
    public const double PromotionMargin = 35;

    private string? _activeId;
    private string? _pendingId;
    private DateTimeOffset _pendingSince;

    public string? ActiveId => _activeId;

    public SelectionResult Evaluate(IReadOnlyCollection<TransportSnapshot> transports, DateTimeOffset now)
    {
        var healthy = transports
            .Where(item => IsHealthy(item, now))
            .OrderByDescending(item => Score(item, now))
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

        var previous = _activeId;
        var active = healthy.FirstOrDefault(item => item.Id == _activeId);
        var best = healthy.FirstOrDefault();

        if (best is null)
        {
            _activeId = null;
            ResetPending();
            return new SelectionResult(previous, null, previous is not null, "Nenhuma conexão saudável");
        }

        if (active is null)
        {
            _activeId = best.Id;
            ResetPending();
            return new SelectionResult(previous, _activeId, previous != _activeId, "Failover imediato");
        }

        if (best.Id == active.Id || Score(best, now) < Score(active, now) + PromotionMargin)
        {
            ResetPending();
            return new SelectionResult(previous, _activeId, false, "Conexão atual mantida");
        }

        if (_pendingId != best.Id)
        {
            _pendingId = best.Id;
            _pendingSince = now;
            return new SelectionResult(previous, _activeId, false, "Aguardando estabilidade da rota melhor");
        }

        if (now - _pendingSince < PromotionDelay)
        {
            return new SelectionResult(previous, _activeId, false, "Aguardando estabilidade da rota melhor");
        }

        _activeId = best.Id;
        ResetPending();
        return new SelectionResult(previous, _activeId, true, "Promovida rota de maior qualidade");
    }

    public static bool IsHealthy(TransportSnapshot item, DateTimeOffset now) =>
        item.IsConnected
        && item.ConsecutiveFailures < 3
        && now - item.LastActivityAt <= ActivityTimeout;

    public static double Score(TransportSnapshot item, DateTimeOffset now)
    {
        var baseScore = item.Kind switch
        {
            TransportKind.Usb => 300d,
            TransportKind.WiFi => 200d,
            TransportKind.Bluetooth => 100d,
            _ => 0d,
        };

        var stabilityBonus = Math.Min(20, Math.Max(0, (now - item.ConnectedAt).TotalSeconds / 3));
        var latencyPenalty = Math.Min(60, Math.Max(0, item.RoundTripMilliseconds) / 10);
        var failurePenalty = item.ConsecutiveFailures * 25;
        return baseScore + stabilityBonus - latencyPenalty - failurePenalty;
    }

    private void ResetPending()
    {
        _pendingId = null;
        _pendingSince = default;
    }
}
