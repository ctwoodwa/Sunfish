import Foundation
import GRDB

/// Top-level coordinator for the W#23 outbound sync engine. Per W#23
/// hand-off Phase 4 + the P4+P4.5 unblock addendum.
///
/// **Substrate v1 scope:** consume `EventQueueServicing.nextPendingBatch`,
/// upload via the singleton `BackgroundUrlSession`, mark rows acked /
/// failed-permanent based on the response. Full URLSession-delegate
/// integration (resumable file-based uploads + the
/// `NSURLErrorBackgroundSessionWasDisconnected` recovery path per ADR
/// 0028-A2.2) ships in subsequent commits on this WIP shelf.
public final class SyncEngine: @unchecked Sendable {
    private let queueService: any EventQueueServicing
    private let bridgeBaseURL: URL
    private let pairingTokenBearer: String?
    private let retryPolicy: RetryPolicy
    private let urlSession: URLSession

    /// Construct a sync engine bound to the local outbound queue + the
    /// Bridge base URL. The supplied `urlSession` is typically the
    /// `BackgroundUrlSession.makeSession(delegate:)` singleton; tests
    /// may inject an `URLSession.shared` for deterministic non-background
    /// behavior.
    public init(
        queueService: any EventQueueServicing,
        bridgeBaseURL: URL,
        pairingTokenBearer: String? = nil,
        retryPolicy: RetryPolicy = .default,
        urlSession: URLSession
    ) {
        self.queueService = queueService
        self.bridgeBaseURL = bridgeBaseURL
        self.pairingTokenBearer = pairingTokenBearer
        self.retryPolicy = retryPolicy
        self.urlSession = urlSession
    }

    /// Drain the next batch of pending events from the queue and POST
    /// each to the Bridge field-event endpoint. Substrate v1 ships
    /// sequential single-event POST; Phase 4+ adds batched + parallel
    /// upload + the file-based resumable path.
    public func drainNextBatch(limit: Int = 50) async throws -> DrainResult {
        let pending = try await queueService.nextPendingBatch(limit: limit)
        var accepted = 0
        var rejected = 0
        for record in pending {
            do {
                try await uploadEventRecord(record)
                try await queueService.markAcked(deviceLocalSeq: record.deviceLocalSeq)
                accepted += 1
            } catch let SyncError.permanent(reason) {
                try await queueService.markFailed(
                    deviceLocalSeq: record.deviceLocalSeq,
                    reason: reason)
                rejected += 1
            } catch {
                // Transient failure: retain row in `pending` for the
                // next drain. Phase 4+ adds the attempt-count increment
                // + retry-policy backoff scheduling.
                continue
            }
        }
        return DrainResult(accepted: accepted, rejected: rejected, pending: pending.count - accepted - rejected)
    }

    /// Upload a single queue record's payload to `POST /api/v1/field/event`.
    /// Throws `SyncError.permanent` for non-retryable failures (4xx
    /// validation errors, 401 auth, 409 idempotency conflicts) and a
    /// generic Error for transient failures (5xx server errors, network
    /// timeouts) so the caller can decide retain-vs-fail-permanent.
    private func uploadEventRecord(_ record: EventQueueRecord) async throws {
        let endpoint = bridgeBaseURL.appendingPathComponent("api/v1/field/event")
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.setValue("application/json; charset=utf-8", forHTTPHeaderField: "Content-Type")
        if let bearer = pairingTokenBearer {
            request.setValue("Bearer \(bearer)", forHTTPHeaderField: "Authorization")
        }
        request.httpBody = record.payload

        let (_, response) = try await urlSession.data(for: request)
        guard let http = response as? HTTPURLResponse else {
            throw SyncError.transient(reason: "non-http-response")
        }
        switch http.statusCode {
        case 200, 201:
            return
        case 400, 401, 409, 422:
            throw SyncError.permanent(reason: "rejected-\(http.statusCode)")
        default:
            throw SyncError.transient(reason: "status-\(http.statusCode)")
        }
    }

    /// Outcome of a single drain pass.
    public struct DrainResult: Equatable, Sendable {
        public let accepted: Int
        public let rejected: Int
        public let pending: Int
    }
}

/// Errors surfaced by the sync engine. Permanent failures transition
/// the queue row to `failed-permanent`; transient failures retain the
/// row in `pending` for the next drain.
public enum SyncError: Error, Sendable, Equatable {
    case permanent(reason: String)
    case transient(reason: String)
}
