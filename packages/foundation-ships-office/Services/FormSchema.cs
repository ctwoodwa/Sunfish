using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.ShipsOffice.Services;

/// <summary>Strongly-typed identifier for a <see cref="FormSchema"/>. UUIDv7.</summary>
/// <remarks>
/// TODO-RELOCATE-WHEN-CANONICAL: replace with
/// <c>Sunfish.Foundation.Forms.FormSchemaId</c> when canonical substrate
/// ships per ADR 0055.
/// </remarks>
public readonly record struct FormSchemaId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static FormSchemaId NewId() => new(Guid.CreateVersion7());
}

/// <summary>
/// Minimal form-schema descriptor — name + status surfaced by the
/// Ship's Office <c>DynamicTemplate</c> kind. Per xo-ruling-T02-43Z,
/// kept minimal pending canonical substrate; consumers should not
/// reach for additional fields (revision history, JSON-schema blob,
/// field-level metadata) until the canonical type ships.
/// </summary>
/// <remarks>
/// TODO-RELOCATE-WHEN-CANONICAL: replace with
/// <c>Sunfish.Foundation.Forms.FormSchema</c> when canonical substrate
/// ships per ADR 0055.
/// </remarks>
public sealed record FormSchema(
    FormSchemaId Id,
    TenantId TenantId,
    string Name,
    FormSchemaStatus Status,
    DateTimeOffset UpdatedAt,
    ActorId LastModifiedBy);

/// <summary>Form-schema lifecycle status — maps to Ship's Office <c>DocumentStatus</c>.</summary>
/// <remarks>
/// TODO-RELOCATE-WHEN-CANONICAL.
/// </remarks>
public enum FormSchemaStatus
{
    Draft,
    Published,
    Archived,
}
