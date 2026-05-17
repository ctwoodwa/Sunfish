using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>
/// Invoice lifecycle state. <b>Overdue is NOT a member</b> — it's derived
/// at read time from <c>(Status == Issued or PartiallyPaid) AND today &gt;
/// DueDate AND Balance &gt; 0</c>. Storing overdue would require a daily
/// rewrite of every aged row; deriving it keeps invoices immutable until
/// a real lifecycle event (payment, void, write-off) lands.
/// </summary>
[JsonConverter(typeof(InvoiceStatusJsonConverter))]
public enum InvoiceStatus
{
    /// <summary>Editable; not yet visible to the customer or the GL.</summary>
    Draft,

    /// <summary>Posted to the GL (Debit AR / Credit Income), visible to the customer, accruing toward DueDate.</summary>
    Issued,

    /// <summary>One or more payments applied; <c>Balance &gt; 0</c>.</summary>
    PartiallyPaid,

    /// <summary>Balance fully applied; <c>Balance == 0</c>. Terminal.</summary>
    Paid,

    /// <summary>Reversed via a Void journal entry. Terminal.</summary>
    Voided,

    /// <summary>Written off as bad debt (Debit BadDebtExpense / Credit AR). Terminal.</summary>
    WrittenOff,
}

/// <summary>Persists <see cref="InvoiceStatus"/> as lowercase camelCase string codes so on-disk payloads survive enum reordering.</summary>
internal sealed class InvoiceStatusJsonConverter : JsonConverter<InvoiceStatus>
{
    public override InvoiceStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "draft"          => InvoiceStatus.Draft,
            "issued"         => InvoiceStatus.Issued,
            "partiallyPaid"  => InvoiceStatus.PartiallyPaid,
            "paid"           => InvoiceStatus.Paid,
            "voided"         => InvoiceStatus.Voided,
            "writtenOff"     => InvoiceStatus.WrittenOff,
            var other        => throw new JsonException($"Unknown InvoiceStatus '{other}'."),
        };

    public override void Write(Utf8JsonWriter writer, InvoiceStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            InvoiceStatus.Draft         => "draft",
            InvoiceStatus.Issued        => "issued",
            InvoiceStatus.PartiallyPaid => "partiallyPaid",
            InvoiceStatus.Paid          => "paid",
            InvoiceStatus.Voided        => "voided",
            InvoiceStatus.WrittenOff    => "writtenOff",
            _ => throw new JsonException($"Unknown InvoiceStatus '{value}'."),
        });
    }
}

/// <summary>Convenience predicates over <see cref="InvoiceStatus"/>.</summary>
public static class InvoiceStatusExtensions
{
    /// <summary>True when the invoice can still receive a payment.</summary>
    public static bool IsOpen(this InvoiceStatus s) =>
        s is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid;

    /// <summary>True for terminal states (Paid / Voided / WrittenOff). No further transitions allowed.</summary>
    public static bool IsTerminal(this InvoiceStatus s) =>
        s is InvoiceStatus.Paid or InvoiceStatus.Voided or InvoiceStatus.WrittenOff;
}
