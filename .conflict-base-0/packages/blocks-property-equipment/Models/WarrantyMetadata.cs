namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>
/// Optional warranty metadata embedded in <see cref="Equipment"/>. Coverage and
/// claim handling are out of scope for the first-slice — this value object
/// just records the dates + provider so downstream blocks (work orders,
/// inspections) can surface "still under warranty" cues.
/// </summary>
public sealed record WarrantyMetadata
{
    /// <summary>Inclusive lower bound on coverage.</summary>
    public required DateTimeOffset StartsAt { get; init; }

    /// <summary>Inclusive upper bound on coverage.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Free-text provider name (e.g. <c>"Manufacturer"</c>, <c>"Best Buy Geek Squad"</c>).</summary>
    public string? Provider { get; init; }

    /// <summary>Provider-issued policy / contract number.</summary>
    public string? PolicyNumber { get; init; }

    /// <summary>Free-text coverage notes (what's covered, exclusions).</summary>
    public string? CoverageNotes { get; init; }
}
