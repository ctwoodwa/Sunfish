using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Temporal;

namespace Sunfish.Foundation.Assets.Hierarchy;

/// <summary>A temporal edge between two entities in the asset graph.</summary>
public sealed record EntityEdge(
    long Id,
    EntityId From,
    EntityId To,
    EdgeKind Kind,
    TemporalRange Validity,
    JsonDocument? Metadata);
