import XCTest
import Foundation
@testable import SunfishField

final class RetryPolicyTests: XCTestCase {
    func testDefault_TenAttempts_ExponentialBackoff() {
        let policy = RetryPolicy.default
        XCTAssertEqual(policy.maxAttempts, 10)
        XCTAssertEqual(policy.initialBackoffSeconds, 1.0)
        XCTAssertEqual(policy.backoffMultiplier, 2.0)
        XCTAssertEqual(policy.maxBackoffSeconds, 600.0)
    }

    func testBackoffSeconds_ExponentialGrowthCappedAtMax() {
        let policy = RetryPolicy.default
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 1), 1.0)
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 2), 2.0)
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 3), 4.0)
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 4), 8.0)
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 10), 512.0)
        // Past attempt 10 the policy refuses to schedule another retry.
        XCTAssertNil(policy.backoffSeconds(forAttempt: 11))
    }

    func testBackoffSeconds_CappedAtMaxBackoff() {
        let policy = RetryPolicy(
            maxAttempts: 20,
            initialBackoffSeconds: 1.0,
            backoffMultiplier: 2.0,
            maxBackoffSeconds: 60.0)
        // 1, 2, 4, 8, 16, 32 — all under 60s.
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 6), 32.0)
        // Attempt 7 raw = 64s; capped at 60s.
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 7), 60.0)
        XCTAssertEqual(policy.backoffSeconds(forAttempt: 8), 60.0)
    }

    func testIsExhausted_TrueAtCap() {
        let policy = RetryPolicy.default
        XCTAssertFalse(policy.isExhausted(after: 0))
        XCTAssertFalse(policy.isExhausted(after: 9))
        XCTAssertTrue(policy.isExhausted(after: 10))
        XCTAssertTrue(policy.isExhausted(after: 11))
    }

    func testBackoffSeconds_AttemptZero_ReturnsNil() {
        let policy = RetryPolicy.default
        XCTAssertNil(policy.backoffSeconds(forAttempt: 0))
    }
}
