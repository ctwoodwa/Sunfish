import Testing
@testable import SunfishField

/// Threshold guard tests for `QueueStatusSnapshot.level`.
///
/// ADR 0028-A2.7 fixes the warning/blocked thresholds at 4 000 / 5 000
/// events. These tests pin the contract so future edits to the switch ranges
/// require an explicit ADR amendment. Council Test-Coverage-B1 (2026-05-13).
@Suite("QueueStatusSnapshot")
struct QueueStatusSnapshotTests {

    // MARK: Threshold constants

    @Test("warningThreshold matches ADR 0028-A2.7 (4 000)")
    func warningThreshold_matchesAdr0028() {
        #expect(QueueStatusSnapshot.warningThreshold == 4_000)
    }

    @Test("blockedThreshold matches ADR 0028-A2.7 (5 000)")
    func blockedThreshold_matchesAdr0028() {
        #expect(QueueStatusSnapshot.blockedThreshold == 5_000)
    }

    // MARK: Level boundaries

    @Test("level is ok when pendingCount is 0")
    func level_ok_atZero() {
        let s = make(count: 0)
        #expect(s.level == .ok)
    }

    @Test("level is ok at warningThreshold - 1 (3 999)")
    func level_ok_belowWarning() {
        let s = make(count: QueueStatusSnapshot.warningThreshold - 1)
        #expect(s.level == .ok)
    }

    @Test("level is warning at warningThreshold (4 000)")
    func level_warning_atWarningThreshold() {
        let s = make(count: QueueStatusSnapshot.warningThreshold)
        #expect(s.level == .warning)
    }

    @Test("level is warning at blockedThreshold - 1 (4 999)")
    func level_warning_belowBlocked() {
        let s = make(count: QueueStatusSnapshot.blockedThreshold - 1)
        #expect(s.level == .warning)
    }

    @Test("level is blocked at blockedThreshold (5 000)")
    func level_blocked_atBlockedThreshold() {
        let s = make(count: QueueStatusSnapshot.blockedThreshold)
        #expect(s.level == .blocked)
    }

    @Test("level is blocked above blockedThreshold")
    func level_blocked_aboveThreshold() {
        let s = make(count: QueueStatusSnapshot.blockedThreshold + 1_000)
        #expect(s.level == .blocked)
    }

    // MARK: isCapacityBlocked

    @Test("isCapacityBlocked is false when ok")
    func isCapacityBlocked_false_whenOk() {
        #expect(!make(count: 0).isCapacityBlocked)
    }

    @Test("isCapacityBlocked is false when warning")
    func isCapacityBlocked_false_whenWarning() {
        #expect(!make(count: QueueStatusSnapshot.warningThreshold).isCapacityBlocked)
    }

    @Test("isCapacityBlocked is true when blocked")
    func isCapacityBlocked_true_whenBlocked() {
        #expect(make(count: QueueStatusSnapshot.blockedThreshold).isCapacityBlocked)
    }

    // MARK: Helpers

    private func make(count: Int) -> QueueStatusSnapshot {
        QueueStatusSnapshot(pendingCount: count, blobBytes: 0, lastSyncDate: nil)
    }
}
