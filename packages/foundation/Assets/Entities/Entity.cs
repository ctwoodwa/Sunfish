using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>
/// The materialized "current" projection of an entity in the asset store.
/// </summary>
/// <remarks>
/// Spec §3.1. Phase A plan D-VERSION-STORE-SHAPE keeps this row alongside the
/// append-only version log so reads are O(1) lookups rather than version-log reductions.
/// </remarks>
public sealed record Entity(
    EntityId Id,
    SchemaId Schema,
    TenantId Tenant,
    VersionId CurrentVersion,
    JsonDocument Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);
