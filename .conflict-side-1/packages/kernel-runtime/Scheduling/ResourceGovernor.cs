using Microsoft.Extensions.Options;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Scheduling;

/// <summary>
/// Default <see cref="IResourceGovernor"/> implementation backed by a
/// <see cref="SemaphoreSlim"/> sized to
/// <see cref="ResourceGovernorOptions.MaxActiveRoundsPerTick"/>. Caps
/// concurrent gossip rounds per ADR 0032 so multi-team installs don't
/// stampede the network and CPU every tick.
/// </summary>
/// <remarks>
/// <para>
/// The semaphore gives FIFO-ish ordering in practice, which is good enough —
/// ADR 0032 does not require strict fairness. The governor is intentionally
/// independent of <c>IGossipDaemon</c>, <c>TeamContext</c>, and transports so
/// the Wave 6.3 per-team factory rewiring can compose it without a circular
/// dependency back into <c>kernel-runtime</c>.
/// </para>
/// </remarks>
public sealed class ResourceGovernor : IResourceGovernor, IDisposable
{
    private readonly SemaphoreSlim _slots;
    private int _disposed;

    /// <summary>
    /// Constructs a governor with the cap from
    /// <paramref name="options"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="ResourceGovernorOptions.MaxActiveRoundsPerTick"/> is less than 1.
    /// </exception>
    public ResourceGovernor(IOptions<ResourceGovernorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var cap = options.Value.MaxActiveRoundsPerTick;
        if (cap < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                cap,
                $"{nameof(ResourceGovernorOptions.MaxActiveRoundsPerTick)} must be >= 1.");
        }

        _slots = new SemaphoreSlim(initialCount: cap, maxCount: cap);
    }

    /// <inheritdoc />
    public async ValueTask<IDisposable> AcquireGossipSlotAsync(TeamId teamId, CancellationToken ct)
    {
        // teamId is reserved for future per-team throttling; see interface XML doc.
        _ = teamId;

        await _slots.WaitAsync(ct).ConfigureAwait(false);
        return new Slot(_slots);
    }

    /// <summary>
    /// Disposes the underlying <see cref="SemaphoreSlim"/>. Slot handles
    /// returned by <see cref="AcquireGossipSlotAsync"/> remain valid but will
    /// no-op on release once the governor itself is disposed.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _slots.Dispose();
        }
    }

    private sealed class Slot : IDisposable
    {
        private readonly SemaphoreSlim _slots;
        private int _released;

        public Slot(SemaphoreSlim slots) => _slots = slots;

        public void Dispose()
        {
            // Idempotent: guard against double-dispose by flipping a one-shot flag.
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            try
            {
                _slots.Release();
            }
            catch (ObjectDisposedException)
            {
                // Governor was disposed before this slot — releasing is moot.
            }
            catch (SemaphoreFullException)
            {
                // Defensive; the one-shot flag above should prevent this.
            }
        }
    }
}
