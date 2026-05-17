namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Receiver-side mirror of the financial cluster's
/// <c>Financial.JournalEntryPosted</c> payload per
/// <c>cross-cluster-event-bus-design.md</c> §3.1 — the work-projects
/// projector consumes a typed snapshot of the upstream payload so the
/// financial cluster contract can evolve without binary-breaking work-
/// projects builds. This record MUST stay compatible with the typed
/// envelope financial publishes (additive evolution; never re-shape).
/// </summary>
/// <param name="EntryId">FK back to the originating JournalEntry; used as <c>sourceRefId</c> on the projected row.</param>
/// <param name="EntryDate">Posting date of the JE (LocalDate-style — UTC-day semantics deferred to financial).</param>
/// <param name="SourceKind">String discriminator — financial-cluster enum stringified; mapped via <c>JournalEntryPostedHandler.MapSourceKind</c>.</param>
/// <param name="Lines">Line-items; the projector iterates these to extract per-project dimensions.</param>
public sealed record JournalEntryPostedPayload(
    Guid EntryId,
    DateOnly EntryDate,
    string SourceKind,
    IReadOnlyList<JournalEntryPostedLine> Lines);

/// <summary>
/// Mirror of a single JE line per
/// <c>cross-cluster-event-bus-design.md</c> §3.1.
/// </summary>
/// <param name="AccountId">FK to a GL account in the financial cluster.</param>
/// <param name="Debit">Debit amount; zero for credit-side lines.</param>
/// <param name="Credit">Credit amount; zero for debit-side lines.</param>
/// <param name="Currency">ISO-4217 3-letter code. Null when financial omits — projector falls back to "USD".</param>
/// <param name="Dimensions">Typed dimension map. The projector reads <c>"projectId"</c> to filter; other dimensions are ignored.</param>
public sealed record JournalEntryPostedLine(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Currency,
    IReadOnlyDictionary<string, string> Dimensions);
