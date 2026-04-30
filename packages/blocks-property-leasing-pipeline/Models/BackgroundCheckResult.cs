namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Outcome of a background check returned by an
/// <c>IBackgroundCheckProvider</c> (Phase 3 wires the interface; Phase 1
/// ships only the entity shape). FCRA-compliant: every cited finding
/// must be presentable on an <see cref="AdverseActionNotice"/> if the
/// finding contributed to a decline.
/// </summary>
public sealed record BackgroundCheckResult
{
    /// <summary>Opaque vendor reference for the upstream report (used for re-fetch + dispute lookup).</summary>
    public required string VendorRef { get; init; }

    /// <summary>The application this report belongs to.</summary>
    public required ApplicationId Application { get; init; }

    /// <summary>Outcome category.</summary>
    public required BackgroundCheckOutcome Outcome { get; init; }

    /// <summary>Per-finding details; empty when <see cref="Outcome"/> is <see cref="BackgroundCheckOutcome.Clear"/>.</summary>
    public required IReadOnlyList<AdverseFinding> Findings { get; init; }

    /// <summary>UTC timestamp the provider returned this report.</summary>
    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>One adverse finding from a <see cref="BackgroundCheckResult"/>; cited verbatim on FCRA notice.</summary>
public sealed record AdverseFinding
{
    /// <summary>Category (e.g., <c>"Eviction"</c>, <c>"Felony"</c>, <c>"CreditDelinquency"</c>).</summary>
    public required string Category { get; init; }

    /// <summary>Free-text description used for FCRA disclosure.</summary>
    public required string Description { get; init; }

    /// <summary>Date the underlying event occurred (per the consumer report).</summary>
    public DateOnly? EventDate { get; init; }

    /// <summary>Source agency / database the finding was sourced from.</summary>
    public required string Source { get; init; }
}
