using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>
/// Bill lifecycle state. AP-specific extensions over AR's Invoice
/// state machine: <see cref="Received"/> is the gateway after Draft (a
/// bill has arrived but may need approval before payment),
/// <see cref="Approved"/> is the optional gate (controlled by
/// <see cref="BlocksFinancialApOptions.ApprovalThreshold"/>), and
/// <see cref="Disputed"/> is a hold that excludes the bill from
/// AP aging without flipping it terminal.
///
/// <para>
/// <b>Overdue is NOT a stored value</b> — derived at read time from
/// <c>(Status.IsOpen() AND today &gt; DueDate AND Balance &gt; 0)</c>.
/// Same rationale as AR's <c>Invoice.IsOverdueAsOf</c>.
/// </para>
/// </summary>
[JsonConverter(typeof(BillStatusJsonConverter))]
public enum BillStatus
{
    /// <summary>Editable; not yet acknowledged by AP.</summary>
    Draft,

    /// <summary>Bill recorded in AP — Dr Expense / Cr AP posted to the GL.</summary>
    Received,

    /// <summary>Approved for payment per workflow policy. Required for payment when <see cref="BlocksFinancialApOptions.ApprovalThreshold"/> is non-null and the bill is at/over threshold.</summary>
    Approved,

    /// <summary>One or more payments applied; <c>Balance &gt; 0</c>.</summary>
    PartiallyPaid,

    /// <summary>Fully paid; <c>Balance == 0</c>. Terminal.</summary>
    Paid,

    /// <summary>Reversed via a Void journal entry. Terminal.</summary>
    Voided,

    /// <summary>Hold — bill is being contested with the vendor. Excluded from AP aging; CRDT-loses to non-Disputed states on merge (a payment that came in concurrently with a Dispute wins).</summary>
    Disputed,
}

internal sealed class BillStatusJsonConverter : JsonConverter<BillStatus>
{
    public override BillStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "draft"         => BillStatus.Draft,
            "received"      => BillStatus.Received,
            "approved"      => BillStatus.Approved,
            "partiallyPaid" => BillStatus.PartiallyPaid,
            "paid"          => BillStatus.Paid,
            "voided"        => BillStatus.Voided,
            "disputed"      => BillStatus.Disputed,
            var other       => throw new JsonException($"Unknown BillStatus '{other}'."),
        };

    public override void Write(Utf8JsonWriter writer, BillStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            BillStatus.Draft          => "draft",
            BillStatus.Received       => "received",
            BillStatus.Approved       => "approved",
            BillStatus.PartiallyPaid  => "partiallyPaid",
            BillStatus.Paid           => "paid",
            BillStatus.Voided         => "voided",
            BillStatus.Disputed       => "disputed",
            _ => throw new JsonException($"Unknown BillStatus '{value}'."),
        });
    }
}

/// <summary>Convenience predicates over <see cref="BillStatus"/>.</summary>
public static class BillStatusExtensions
{
    /// <summary>True for non-terminal, non-Draft states with potential balance — i.e., visible on AP dashboards.</summary>
    public static bool IsOpen(this BillStatus s) =>
        s is BillStatus.Received or BillStatus.Approved or BillStatus.PartiallyPaid;

    /// <summary>True for terminal states. No further transitions allowed.</summary>
    public static bool IsTerminal(this BillStatus s) =>
        s is BillStatus.Paid or BillStatus.Voided;

    /// <summary>True if the bill is in a state where a payment can be applied (subject to approval-gate policy).</summary>
    public static bool IsPayable(this BillStatus s) =>
        s is BillStatus.Received or BillStatus.Approved or BillStatus.PartiallyPaid;
}
