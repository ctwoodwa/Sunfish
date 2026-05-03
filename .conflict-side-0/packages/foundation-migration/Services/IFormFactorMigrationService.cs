using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// The migration-semantics contract per ADR 0028-A5.8. Phase 2 ships
/// <see cref="ComputeDerivedSurfaceAsync"/>; <see cref="ApplyMigrationAsync"/>
/// ships in Phase 3. <c>EnrollAsync</c> (QR-onboarding handshake per
/// A5.7) is gated on ~ADR-0032-A1 ratification per the W#35 hand-off
/// halt-condition; ships with the rest of the consumer surface in a
/// later workstream.
/// </summary>
public interface IFormFactorMigrationService
{
    /// <summary>
    /// Recomputes the derived surface from a form-factor profile + the
    /// workspace's declared capability set. The output names which
    /// capabilities are visible (active surface) and which are excluded
    /// (will sequester in P3).
    /// </summary>
    ValueTask<DerivedSurface> ComputeDerivedSurfaceAsync(
        FormFactorProfile profile,
        IReadOnlySet<string> workspaceDeclaredCapabilities,
        CancellationToken ct = default);

    /// <summary>
    /// Applies sequestration / release transitions per A5.2 + A5.4 +
    /// A8.3 (rules 5–7). Phase 3.
    /// </summary>
    ValueTask ApplyMigrationAsync(
        HardwareTierChangeEvent change,
        CancellationToken ct = default);
}
