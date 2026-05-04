import Foundation

/// Retry policy for outbound event + blob uploads. Per W#23 hand-off
/// Phase 4: exponential backoff capped at `maxAttempts`; rows transition
/// to `failed-permanent` after the cap.
public struct RetryPolicy: Sendable, Equatable {
    /// Hard cap on retry attempts before transitioning a queue row to
    /// `failed-permanent`. Substrate v1 default; configurable via host.
    public let maxAttempts: Int
    /// Initial backoff delay in seconds before the first retry.
    public let initialBackoffSeconds: Double
    /// Multiplier applied to the backoff delay between attempts.
    public let backoffMultiplier: Double
    /// Hard upper bound on the per-attempt backoff (capped exponential).
    public let maxBackoffSeconds: Double

    public init(
        maxAttempts: Int = 10,
        initialBackoffSeconds: Double = 1.0,
        backoffMultiplier: Double = 2.0,
        maxBackoffSeconds: Double = 600.0
    ) {
        self.maxAttempts = maxAttempts
        self.initialBackoffSeconds = initialBackoffSeconds
        self.backoffMultiplier = backoffMultiplier
        self.maxBackoffSeconds = maxBackoffSeconds
    }

    /// Substrate v1 default — 10 attempts, 1s → 2s → 4s → ... capped at 600s.
    public static let `default` = RetryPolicy()

    /// Compute the backoff delay for the given attempt count (1-based;
    /// `attempt == 1` is the first retry after the initial failure).
    /// Returns `nil` when `attempt` exceeds `maxAttempts` — the caller
    /// transitions the row to `failed-permanent`.
    public func backoffSeconds(forAttempt attempt: Int) -> Double? {
        guard attempt >= 1, attempt <= maxAttempts else { return nil }
        let raw = initialBackoffSeconds * pow(backoffMultiplier, Double(attempt - 1))
        return min(raw, maxBackoffSeconds)
    }

    /// Returns true when the row should transition to `failed-permanent`
    /// after the supplied attempt count.
    public func isExhausted(after attempt: Int) -> Bool {
        attempt >= maxAttempts
    }
}
