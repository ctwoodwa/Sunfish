using System.Collections.Generic;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Search query for <see cref="IShipsOfficeDataProvider.SearchAsync"/> per
/// ADR 0083 §1. Empty filters mean "no restriction"; null
/// <see cref="TextQuery"/> means "no text match required".
/// </summary>
/// <param name="TextQuery">Free-form text query; null skips text matching.</param>
/// <param name="KindFilter">Restrict to these document kinds; null skips the filter.</param>
/// <param name="StatusFilter">Restrict to a single status; null skips the filter.</param>
/// <param name="PageSize">Max results per page; defaults to 50.</param>
/// <param name="PageToken">Opaque continuation token from a prior page; null for the first page.</param>
public sealed record ShipsOfficeSearchQuery(
    string? TextQuery,
    IReadOnlyList<ShipsOfficeDocumentKind>? KindFilter,
    DocumentStatus? StatusFilter,
    int PageSize = 50,
    string? PageToken = null);
