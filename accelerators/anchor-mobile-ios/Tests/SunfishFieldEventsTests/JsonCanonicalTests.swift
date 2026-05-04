import XCTest
import Foundation
@testable import SunfishField

/// Tests for the pragmatic canonical-JSON serializer mirroring the .NET
/// `Sunfish.Foundation.Crypto.CanonicalJson` implementation.
///
/// **Cross-language byte parity** is verified by `recanonicalize` against
/// 10 hand-written fixtures whose expected output matches what the .NET
/// `CanonicalJson.Serialize` produces for the equivalent .NET CLR object.
/// The fixture corpus is small enough to be auditable — each entry's
/// expected bytes can be cross-checked against the .NET reference by
/// running the corresponding `CanonicalJsonTests` fixture in
/// `packages/foundation/tests/Crypto/CanonicalJsonTests.cs`.
final class JsonCanonicalTests: XCTestCase {
    // ===== Round-trip stability =====

    func testRecanonicalize_SortedKeys_AlphabeticalOrdinalOrder() throws {
        let input = Data(#"{"b":1,"a":2,"c":3}"#.utf8)
        let canonical = try JsonCanonical.recanonicalize(input)
        XCTAssertEqual(String(data: canonical, encoding: .utf8), #"{"a":2,"b":1,"c":3}"#)
    }

    func testRecanonicalize_NestedObjectKeys_SortedRecursively() throws {
        let input = Data(#"{"outer":{"z":1,"a":2}}"#.utf8)
        let canonical = try JsonCanonical.recanonicalize(input)
        XCTAssertEqual(
            String(data: canonical, encoding: .utf8),
            #"{"outer":{"a":2,"z":1}}"#)
    }

    func testRecanonicalize_ArrayOrderPreserved() throws {
        let input = Data(#"[3,1,2]"#.utf8)
        let canonical = try JsonCanonical.recanonicalize(input)
        XCTAssertEqual(String(data: canonical, encoding: .utf8), #"[3,1,2]"#)
    }

    func testRecanonicalize_NoWhitespace() throws {
        let input = Data(#"{ "a" : 1 ,  "b" : 2 }"#.utf8)
        let canonical = try JsonCanonical.recanonicalize(input)
        XCTAssertEqual(String(data: canonical, encoding: .utf8), #"{"a":1,"b":2}"#)
    }

    func testRecanonicalize_IsIdempotent() throws {
        let input = Data(#"{"b":1,"a":2}"#.utf8)
        let once = try JsonCanonical.recanonicalize(input)
        let twice = try JsonCanonical.recanonicalize(once)
        XCTAssertEqual(once, twice)
    }

    // ===== Encodable surface =====

    func testSerialize_Encodable_StructWithSortedKeys() throws {
        struct Probe: Codable, Equatable {
            let bee: Int
            let alpha: String
        }
        let probe = Probe(bee: 1, alpha: "x")
        let bytes = try JsonCanonical.serialize(probe)
        // sortedKeys output should produce alpha before bee.
        XCTAssertEqual(String(data: bytes, encoding: .utf8), #"{"alpha":"x","bee":1}"#)
    }

    // ===== Cross-language fixture parity =====

    /// Each fixture pairs a JSON input with the byte stream the
    /// .NET CanonicalJson produces. Fixtures are hand-written; cross-
    /// reference against the .NET reference test suite when extending.
    private static let crossLangFixtures: [(input: String, expected: String)] = [
        // Empty object / array.
        ("{}", "{}"),
        ("[]", "[]"),
        // Simple primitives.
        ("\"hello\"", "\"hello\""),
        ("42", "42"),
        ("true", "true"),
        // Sorted keys at the top level.
        (#"{"b":1,"a":2}"#, #"{"a":2,"b":1}"#),
        // Sorted keys recursively.
        (#"{"o":{"z":1,"a":2}}"#, #"{"o":{"a":2,"z":1}}"#),
        // Whitespace stripped.
        (#"{ "k" : "v" }"#, #"{"k":"v"}"#),
        // Array elements preserved.
        (#"[3,1,4,1,5,9]"#, #"[3,1,4,1,5,9]"#),
        // Nested mix.
        (#"{"x":[{"b":1,"a":2},{"d":4,"c":3}]}"#, #"{"x":[{"a":2,"b":1},{"c":3,"d":4}]}"#),
    ]

    func testCrossLanguageFixtures_AllProduceMatchingBytes() throws {
        for (idx, fixture) in Self.crossLangFixtures.enumerated() {
            let inputBytes = Data(fixture.input.utf8)
            let canonical = try JsonCanonical.recanonicalize(inputBytes)
            let actual = String(data: canonical, encoding: .utf8) ?? ""
            XCTAssertEqual(actual, fixture.expected,
                "Fixture #\(idx) (input: \(fixture.input)) — mismatch with .NET CanonicalJson reference")
        }
    }
}
