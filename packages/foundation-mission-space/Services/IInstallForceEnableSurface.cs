using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Per ADR 0063-A1.11 — operator-only override surface for install-time
/// blocks. Emits <c>InstallForceEnabled</c> with shape parity to W#40's
/// <c>FeatureForceEnabled</c> (operator_principal_id + reason +
/// override_targets) per Phase 3 halt-condition #5.
/// </summary>
/// <remarks>
/// <para>
/// Operator-role authorization is the host's responsibility (typically
/// wired via authorization middleware that verifies the caller is in the
/// operator role before invoking <see cref="RequestAsync"/>). The surface
/// only enforces the substrate-level invariant: <see cref="InstallForceRequest.Reason"/>
/// must not be empty (justification text per A1.11 council fix).
/// </para>
/// </remarks>
public interface IInstallForceEnableSurface
{
    /// <summary>Records an operator-issued install force-enable per A1.11.</summary>
    ValueTask<InstallForceRecord> RequestAsync(InstallForceRequest request, CancellationToken ct = default);
}

/// <summary>Operator's install force-enable request per A1.11.</summary>
public sealed record InstallForceRequest
{
    public required string OperatorPrincipalId { get; init; }

    /// <summary>Per A1.11 — justification text; MUST NOT be empty.</summary>
    public required string Reason { get; init; }

    /// <summary>Per A1.11 — the dimensions whose evaluation failures the operator is overriding.</summary>
    public required IReadOnlyList<DimensionChangeKind> OverrideTargets { get; init; }

    /// <summary>Envelope hash at the time of override (links to the evaluated <see cref="MissionEnvelope"/>).</summary>
    public required string EnvelopeHash { get; init; }

    /// <summary>Optional platform key; matches the platform used during evaluation.</summary>
    public string? Platform { get; init; }
}

/// <summary>Recorded operator install force-enable — returned from <see cref="IInstallForceEnableSurface.RequestAsync"/>.</summary>
public sealed record InstallForceRecord
{
    public required string OperatorPrincipalId { get; init; }

    public required string Reason { get; init; }

    public required IReadOnlyList<DimensionChangeKind> OverrideTargets { get; init; }

    public required string EnvelopeHash { get; init; }

    public string? Platform { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }
}
