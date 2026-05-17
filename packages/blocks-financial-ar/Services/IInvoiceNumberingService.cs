using System.Text.RegularExpressions;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// Mints monotonic, customer-facing invoice numbers and arbitrates
/// post-sync number collisions between replicas. Numbers follow the
/// format <c>INV-YYYY-MM-DD-{ReplicaId}-{seq:D4}</c>:
///
/// <list type="bullet">
/// <item><c>YYYY-MM-DD</c>: <see cref="DateOnly"/> passed to <see cref="NextNumberAsync"/> — usually the invoice's <c>IssueDate</c>.</item>
/// <item><c>{ReplicaId}</c>: this install's per-replica suffix (configured via <see cref="Models.BlocksFinancialArOptions.LocalReplicaId"/>).</item>
/// <item><c>{seq:D4}</c>: per-<c>(ChartOfAccountsId, ReplicaId)</c> monotonic counter, zero-padded to 4 digits; expands beyond 4 digits at 10000+.</item>
/// </list>
///
/// <para>
/// <b>The sequence is NOT per-day.</b> It's per-(chart, replica) and
/// monotonic across all dates. A re-key after a collision (see
/// <see cref="ResolveCollisionAsync"/>) advances the counter; gaps are
/// allowed.
/// </para>
/// </summary>
public interface IInvoiceNumberingService
{
    /// <summary>
    /// Mint the next invoice number for the given <paramref name="chartId"/>
    /// on this replica. <paramref name="issueDate"/> is embedded in the
    /// number prefix; the counter is independent of date.
    /// </summary>
    Task<string> NextNumberAsync(
        ChartOfAccountsId chartId,
        DateOnly issueDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deterministic arbiter for a post-sync number collision. When two
    /// replicas independently mint the same number (different invoices,
    /// same printed string), the one that MUST re-key is returned —
    /// callers re-mint that replica's invoice with a fresh number.
    ///
    /// <para>
    /// Rule: <b>older replica wins.</b> The replica with the earlier
    /// <c>CreatedAt</c> keeps the number; the younger replica re-keys.
    /// Tie (equal timestamps): lexicographic comparison of the
    /// <see cref="ReplicaId.Value"/> strings — smaller wins.
    /// </para>
    /// </summary>
    Task<ReplicaId> ResolveCollisionAsync(
        ChartOfAccountsId chartId,
        string conflictingNumber,
        ReplicaId localReplica,
        ReplicaId remoteReplica,
        Instant localReplicaCreatedAt,
        Instant remoteReplicaCreatedAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Format helpers for invoice numbers. Decoupled from
/// <see cref="IInvoiceNumberingService"/> instances so the repository
/// can validate without taking a service dep.
/// </summary>
public static class InvoiceNumberFormat
{
    // Matches `INV-2026-05-17-{REPLICA}-{NNNN+}`. Replica suffix is one or
    // more uppercase alphanumeric characters; sequence is at least 4
    // digits but may expand beyond when the counter overflows D4.
    private static readonly Regex Pattern =
        new(@"^INV-\d{4}-\d{2}-\d{2}-[A-Z0-9]+-\d{4,}$", RegexOptions.Compiled);

    /// <summary>True iff <paramref name="invoiceNumber"/> matches the canonical format.</summary>
    public static bool IsWellFormed(string? invoiceNumber) =>
        invoiceNumber is not null && Pattern.IsMatch(invoiceNumber);
}
