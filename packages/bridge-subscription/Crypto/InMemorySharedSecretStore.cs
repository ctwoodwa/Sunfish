using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// In-memory <see cref="ISharedSecretStore"/> with rotation grace per
/// ADR 0031-A1.12.1. Substrate Phase 1; production hosts wrap with
/// the ADR 0046 foundation-recovery field-encryption substrate for
/// at-rest encryption.
/// </summary>
public sealed class InMemorySharedSecretStore : ISharedSecretStore
{
    /// <summary>90-day default rotation cadence per A1.12.1.</summary>
    public static readonly TimeSpan DefaultRotationCadence = TimeSpan.FromDays(90);

    /// <summary>24-hour grace window during which the previous secret remains valid per A1.12.1.</summary>
    public static readonly TimeSpan DefaultGraceWindow = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, Entry> _store = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _graceWindow;

    public InMemorySharedSecretStore(TimeProvider? time = null, TimeSpan? graceWindow = null)
    {
        _time = time ?? TimeProvider.System;
        _graceWindow = graceWindow ?? DefaultGraceWindow;
    }

    /// <inheritdoc />
    public ValueTask<SharedSecretLookup> ResolveAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ct.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(tenantId, out var entry))
        {
            return ValueTask.FromResult(new SharedSecretLookup());
        }
        var now = _time.GetUtcNow();
        var prev = entry.PreviousSecret is not null && now - entry.RotatedAt < _graceWindow
            ? entry.PreviousSecret
            : null;
        return ValueTask.FromResult(new SharedSecretLookup
        {
            Current = entry.CurrentSecret,
            PreviousInGrace = prev,
        });
    }

    /// <inheritdoc />
    public ValueTask StageRotationAsync(string tenantId, string newSecret, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentException.ThrowIfNullOrEmpty(newSecret);
        ct.ThrowIfCancellationRequested();

        _store.AddOrUpdate(
            tenantId,
            _ => new Entry(newSecret, PreviousSecret: null, RotatedAt: _time.GetUtcNow()),
            (_, existing) => new Entry(newSecret, PreviousSecret: existing.CurrentSecret, RotatedAt: _time.GetUtcNow()));
        return ValueTask.CompletedTask;
    }

    private sealed record Entry(string CurrentSecret, string? PreviousSecret, DateTimeOffset RotatedAt);
}
