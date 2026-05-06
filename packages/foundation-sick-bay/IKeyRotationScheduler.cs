using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Net-new abstraction layer between <see cref="ISickBayCommandService"/>
/// and the W#32 / ADR 0046-A2 key-rotation substrate per ADR 0082.
/// Phase 1 ships the contract; Phase 2 lands the
/// <c>NoopKeyRotationScheduler</c> stub; Phase 3b lands the real
/// implementation that calls into the W#32 rotation pipeline.
/// </summary>
public interface IKeyRotationScheduler
{
    /// <summary>
    /// Schedule a key-rotation for <paramref name="fieldPurpose"/>.
    /// Phase 2 stub returns <see cref="Task.CompletedTask"/>; Phase 3b
    /// hands the request off to the W#32 / ADR 0046-A2 substrate.
    /// </summary>
    Task ScheduleAsync(
        TenantId tenant,
        string fieldPurpose,
        string triggerReason,
        CancellationToken ct = default);
}
