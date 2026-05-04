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
/// **Cross-language byte parity** is verified by a 10-fixture test suite
/// (`Tests/SunfishFieldEventsTests/JsonCanonicalCrossLangTests.swift`):
/// each fixture pairs a JSON input with the byte stream produced by the
/// .NET implementation; the Swift output must match byte-for-byte.
public enum JsonCanonical {
    /// Serialize an `Encodable` value to canonical-JSON UTF-8 bytes.
    /// Date encoding follows the `.iso8601` strategy (matching the .NET
    /// `DateTimeOffset.ToString("O")` round-trip format).
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
