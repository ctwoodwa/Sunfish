using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reports;

/// <summary>
/// Execution context passed to every <see cref="IReportCartridge{TParams,TResult}"/>
/// invocation. Carries tenant scope, an opaque snapshot marker, the
/// run wall-clock, and the principal that requested the report.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant isolation invariant.</b> Every cartridge MUST treat
/// <see cref="TenantId"/> as the sole tenant scope for its execution;
/// cartridge parameters that include entity IDs (property, customer,
/// vendor, account) MUST validate those IDs belong to the same
/// tenant — cross-tenant leakage is the only structural risk on the
/// read-side.
/// </para>
/// <para>
/// <b>Snapshot marker.</b> <see cref="SnapshotMarker"/> is opaque to
/// cartridges and passed verbatim to upstream cluster read APIs.
/// When the per-cluster marker honor lands in a future hand-off,
/// cartridges automatically get coherent snapshots without any code
/// change at this layer (upstream read APIs currently ignore the
/// marker argument).
/// </para>
/// <para>
/// <b>Wall-clock.</b> <see cref="AsOfUtc"/> uses
/// <see cref="System.DateTimeOffset"/> per cohort precedent (W#34 /
/// W#35 / W#40 / W#41 / W#49) — the hand-off cited
/// <c>NodaTime.Instant</c>, but <c>NodaTime</c> is not on
/// <c>Directory.Packages.props</c>. Migration to <c>Instant</c>
/// would land via a single follow-up ADR amendment touching every
/// <c>Sunfish.Foundation.*</c> + cluster time-bearing record at
/// once.
/// </para>
/// </remarks>
/// <param name="TenantId">The sole tenant scope for this cartridge execution.</param>
/// <param name="SnapshotMarker">Opaque marker forwarded to upstream cluster read APIs.</param>
/// <param name="AsOfUtc">Wall-clock at the start of the report run.</param>
/// <param name="RequestedBy">The principal that requested the report.</param>
public sealed record ReportExecutionContext(
    TenantId TenantId,
    string SnapshotMarker,
    System.DateTimeOffset AsOfUtc,
    PrincipalId RequestedBy);
