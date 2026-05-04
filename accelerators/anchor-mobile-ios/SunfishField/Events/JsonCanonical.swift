import Foundation

/// Pragmatic canonical-JSON serializer for cross-replica byte stability.
///
/// **Mirrors `Sunfish.Foundation.Crypto.CanonicalJson`** (the .NET-side
/// implementation at `packages/foundation/Crypto/CanonicalJson.cs`) — both
/// produce the same byte stream from logically-equal JSON trees:
///
/// 1. Object keys sorted alphabetically (ordinal / UTF-16 code unit order).
/// 2. Array element order preserved.
/// 3. No whitespace between tokens.
/// 4. UTF-8 output, no BOM.
///
/// The .NET side documents itself as "pragmatic, not full RFC 8785 JCS";
/// this Swift mirror inherits that scope. Number formatting + string
/// escaping defer to the platform JSON encoders (`JSONEncoder` here,
/// `System.Text.Json` on .NET) — both follow the JSON RFC 8259 spec for
/// scalar value representation, so simple cases (integers, decimal
/// fractions, ASCII strings) round-trip identically.
///
/// **Cross-language byte parity** is sanity-checked by a 10-fixture
/// suite in `Tests/SunfishFieldEventsTests/JsonCanonicalTests.swift`:
/// each fixture pairs a JSON input with the byte stream the .NET
/// implementation produces for the equivalent CLR object. The corpus
/// is hand-derived from the .NET `CanonicalJson` sort + no-whitespace
/// rules (NOT a programmatic round-trip against the .NET reference);
/// non-ASCII keys + number edge cases + Date round-trip are deferred to
/// follow-up fixtures when the .NET cross-check pipeline lands.
public enum JsonCanonical {
    /// Serialize an `Encodable` value to canonical-JSON UTF-8 bytes.
    ///
    /// FIXME(W#23-P4): the `.iso8601` date encoding strategy emits
    /// `2026-05-04T12:34:56Z` (second-precision, no fractional) while
    /// .NET `DateTimeOffset.ToString("O")` emits
    /// `2026-05-04T12:34:56.7890123+00:00` (7-digit fractional + offset).
    /// For substrate v1 this is OK because (a) merge-boundary
    /// verification of envelope canonical bytes hasn't shipped and
    /// (b) the .NET `SerializeSignable` envelope path is .NET-only
    /// today. Before P4 ships the sync engine the merge boundary
    /// verifies, swap in a custom `.formatted(...)` strategy mirroring
    /// `"O"` (7-digit fractional + `+00:00`), or normalize `Date` →
    /// `String` upstream of `JsonCanonical.serialize` so byte parity
    /// holds across replicas.
    public static func serialize<T: Encodable>(_ value: T) throws -> Data {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        return try encoder.encode(value)
    }

    /// Re-canonicalize an already-encoded JSON byte stream — parses it,
    /// re-emits with sorted keys + no whitespace + UTF-8. Used by the
    /// outbound sync engine when re-encoding stored envelopes for upload.
    public static func recanonicalize(_ jsonBytes: Data) throws -> Data {
        let object = try JSONSerialization.jsonObject(
            with: jsonBytes,
            options: [.fragmentsAllowed])
        return try JSONSerialization.data(
            withJSONObject: object,
            options: [.sortedKeys, .fragmentsAllowed, .withoutEscapingSlashes])
    }
}
