using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// One entry in the sequestration partition per ADR 0028-A5.4 +
/// A8.3. Tracks a record (or a single field within a record) that is
/// known to the sequestration store, alongside the capability it
/// requires + the encryption-shape flags that drive the
/// plaintext-vs-ciphertext + record-vs-field choice in
/// <see cref="IFormFactorMigrationService.ApplyMigrationAsync"/>.
/// </summary>
public sealed record SequesteredRecord
{
    /// <summary>The host this entry is tracked on (typically the local node id).</summary>
    [JsonPropertyName("nodeId")]
    public required string NodeId { get; init; }

    /// <summary>Stable id of the record (or, for a field-level entry, "{recordId}#{fieldName}").</summary>
    [JsonPropertyName("recordId")]
    public required string RecordId { get; init; }

    /// <summary>The capability tag (per <see cref="InMemoryFormFactorMigrationService.DeriveHostCapabilities"/>) the record / field requires.</summary>
    [JsonPropertyName("requiredCapability")]
    public required string RequiredCapability { get; init; }

    /// <summary>
    /// Whether the record is encrypted-at-rest. Drives the A8.3 rule 5
    /// flag selection: encrypted → <see cref="SequestrationFlagKind.CiphertextSequestered"/>,
    /// plaintext → <see cref="SequestrationFlagKind.PlaintextSequestered"/>.
    /// </summary>
    [JsonPropertyName("isEncrypted")]
    public required bool IsEncrypted { get; init; }

    /// <summary>
    /// Whether the record's primary-key or display-name fields are
    /// encrypted (per A8.3 rule 7). When <c>true</c>, the entire record
    /// is record-level-sequestered. When <c>false</c>, only individual
    /// un-decryptable fields are redacted (field-level); the record
    /// itself stays visible.
    /// </summary>
    [JsonPropertyName("isPrimaryKeyEncrypted")]
    public required bool IsPrimaryKeyEncrypted { get; init; }

    /// <summary>
    /// Whether this record is a CP-class consensus record. When
    /// sequestered (per A8.3 rule 6), the host's vote is ineligible
    /// for this record's quorum.
    /// </summary>
    [JsonPropertyName("isCpClass")]
    public required bool IsCpClass { get; init; }

    /// <summary>The current sequestration flag, or null if the record is active (not sequestered).</summary>
    [JsonPropertyName("flag")]
    [JsonConverter(typeof(JsonStringEnumConverter<SequestrationFlagKind>))]
    public SequestrationFlagKind? Flag { get; init; }
}
