using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SickBay;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// Phase 2 stub <see cref="IKeyRotationScheduler"/> per ADR 0082 +
/// W#54 Phase 2. Returns <see cref="Task.CompletedTask"/> without
/// scheduling any rotation. Phase 3b will replace this with the real
/// implementation that hands off to the W#32 / ADR 0046-A2 rotation
/// substrate.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR 0082-A1 WARNING:</b> This stub completes successfully without scheduling
/// any rotation. Hosts MUST NOT register this implementation in any environment
/// that surfaces a user-visible confirmation ("rotation triggered") — doing so
/// creates a false security assurance. This class is intended for build-phase
/// scaffolding only; replace with the real scheduler before any UI wiring.
/// </para>
/// The Phase-2 ledger row note for W#54 calls out this stub explicitly;
/// Phase 3b PR description MUST flag the swap so audit-event flow can
/// be re-verified.
/// </remarks>
internal sealed class NoopKeyRotationScheduler : IKeyRotationScheduler
{
    /// <inheritdoc />
    public Task ScheduleAsync(
        TenantId tenant,
        string fieldPurpose,
        string triggerReason,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
