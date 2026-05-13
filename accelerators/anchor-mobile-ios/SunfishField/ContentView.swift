import SwiftUI

/// Root routing view. Phase 5 (pairing flow) owns the `isPaired` gate;
/// Phase 6 ships the `HomeView` target. Pre-Phase-5 builds always show
/// `HomeView` with a stub queue service so the home screen is reviewable
/// in the simulator without a real Anchor pairing.
struct ContentView: View {
    @State private var isPaired = true   // Phase 5 replaces with persistent pairing state.

    var body: some View {
        if isPaired {
            HomeView(
                queueService: StubEventQueueService(),
                syncEngine: SyncEngine(
                    queueService: StubEventQueueService(),
                    bridgeBaseURL: URL(string: "https://bridge.local")!,
                    urlSession: .shared))
        } else {
            // Phase 5 replaces with PairingFlowView.
            Text("Sunfish Field — tap to pair")
                .onTapGesture { isPaired = true }
        }
    }
}

// MARK: - StubEventQueueService

/// Minimal no-op implementation used in simulator / pre-Phase-5 builds.
/// Returns empty state so HomeView renders the "No events queued" baseline.
private final class StubEventQueueService: EventQueueServicing, @unchecked Sendable {
    func appendAsync(envelope: EventEnvelope) async throws {}
    func nextPendingBatch(limit: Int) async throws -> [EventQueueRecord] { [] }
    func markAcked(deviceLocalSeq: Int64) async throws {}
    func markFailed(deviceLocalSeq: Int64, reason: String) async throws {}
    func pendingCount() async throws -> Int { 0 }
    func pendingBlobBytes() async throws -> Int64 { 0 }
}
