using System.Collections.Generic;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Aggregate read-side projection returned by
/// <c>IIntegrationAtlasProvider.GetAtlasViewAsync</c> per
/// ADR 0067 §3.6. Combines per-category active-provider snapshots
/// + per-category validation status + per-provider validation
/// history + tenant email routing into one entry-point payload
/// for the Atlas integration-config UI surface.
/// </summary>
/// <remarks>
/// <b>Side-effect-free contract (inherited from
/// <see cref="IAtlasProvider{TView}"/>):</b> implementations MUST
/// be projection-only — no mutations, no audit emission, no
/// Standing-Order issuance. The Helm shell tick may call this
/// many times per second; fast projection over the latest
/// committed Standing-Order log is required.
/// </remarks>
/// <param name="ActiveByCategory">
/// Most-recent active-provider snapshot per category. A category
/// with no provider configured is absent from the dictionary
/// (NOT present with a null value).
/// </param>
/// <param name="StatusByCategory">
/// Most-recent validation status per category — drives the
/// status badge rendered on each category tile.
/// </param>
/// <param name="CredentialsByProvider">
/// Per-category validation history (newest first), keyed by
/// category. Used by the trend pane to show recent
/// reachability + credential-validity status. Renders empty list
/// for categories with no validation history.
/// </param>
/// <param name="EmailRouting">
/// Tenant-scoped email routing for transactional + marketing
/// senders. Null when the tenant has no email routing configured.
/// </param>
public sealed record IntegrationAtlasView(
    IReadOnlyDictionary<IntegrationCategory, ActiveProviderSnapshot?> ActiveByCategory,
    IReadOnlyDictionary<IntegrationCategory, ProviderValidationStatus> StatusByCategory,
    IReadOnlyDictionary<IntegrationCategory, IReadOnlyList<ProviderValidationStatusEntry>> CredentialsByProvider,
    IntegrationEmailRouting? EmailRouting);
