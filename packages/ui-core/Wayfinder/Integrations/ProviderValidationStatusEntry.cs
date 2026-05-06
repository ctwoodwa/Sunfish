using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Single entry in the validation-status history per ADR 0067 §3.13.
/// <see cref="IValidationStatusStore.HistoryAsync"/> streams these
/// for the UI's "validation history" detail pane.
/// </summary>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="Category">Integration category.</param>
/// <param name="ProviderId">Provider identifier.</param>
/// <param name="Result">Validation outcome at <paramref name="RecordedAt"/>.</param>
/// <param name="RecordedBy">Actor that triggered the validation.</param>
/// <param name="RecordedAt">Wall-clock time the entry was recorded.</param>
public sealed record ProviderValidationStatusEntry(
    TenantId TenantId,
    IntegrationCategory Category,
    string ProviderId,
    IntegrationValidationResult Result,
    ActorId RecordedBy,
    DateTimeOffset RecordedAt);
