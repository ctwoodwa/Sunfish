using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Read-only snapshot of the currently-active provider for a single
/// <see cref="IntegrationCategory"/> per ADR 0067 §3.6. Surfaces in
/// <see cref="IntegrationAtlasView.ActiveByCategory"/>; the Atlas
/// integration-config UI uses this to display "active provider:
/// X (activated by Y on Z)" without re-querying the Standing-Order
/// log.
/// </summary>
/// <remarks>
/// <b>Provenance traceability:</b>
/// <see cref="ActivationOrderId"/> points to the durable Standing
/// Order that activated this provider — every active-provider
/// projection traces back to a signed, audited Standing Order. The
/// rotation-non-destruction invariant (§7.1) means prior provider
/// records remain in the Standing-Order log even after rotation;
/// this snapshot only carries the most-recent activation.
/// Per cohort precedent, <see cref="DateTimeOffset"/> stands in for
/// the hand-off's <c>NodaTime.Instant</c> — NodaTime is not on
/// Directory.Packages.props.
/// </remarks>
/// <param name="ProviderId">Stable provider id (e.g., <c>"stripe"</c>, <c>"sendgrid"</c>).</param>
/// <param name="ActivatedAt">Wall-clock timestamp of activation.</param>
/// <param name="ActivatedBy">Actor who issued the activation Standing Order.</param>
/// <param name="ActivationOrderId">Standing-Order id that activated this provider.</param>
public sealed record ActiveProviderSnapshot(
    string ProviderId,
    DateTimeOffset ActivatedAt,
    ActorId ActivatedBy,
    StandingOrderId ActivationOrderId);
