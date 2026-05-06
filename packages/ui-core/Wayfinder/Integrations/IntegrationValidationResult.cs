using System;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Outcome of <see cref="IIntegrationProviderValidator.ValidateAsync"/>
/// per ADR 0067 §3.8. Stored in <see cref="IValidationStatusStore"/>;
/// rendered as a status badge with optional error context.
/// </summary>
/// <remarks>
/// <see cref="DateTimeOffset"/> per cohort precedent (W#46 / W#49 / W#50
/// / W#54 / W#55 / W#53 / W#46 P2a) — NodaTime is not on
/// <c>Directory.Packages.props</c>; future ADR amendment will migrate
/// every cohort time-bearing record at once.
/// </remarks>
/// <param name="Status">Discriminator for the validation outcome.</param>
/// <param name="ValidatedAt">Wall-clock time the validation ran.</param>
/// <param name="ErrorCode">Stable error code when <paramref name="Status"/> is non-Valid; null on success.</param>
/// <param name="ErrorMessage">Localized human-readable error message; null on success.</param>
public sealed record IntegrationValidationResult(
    ProviderValidationStatus Status,
    DateTimeOffset ValidatedAt,
    string? ErrorCode,
    string? ErrorMessage);
