import SwiftUI

/// Queue-status summary row shown at the bottom of `HomeView`.
///
/// Displays pending event count, approximate blob storage, and last successful
/// sync timestamp. Color + icon–coded per ADR 0028-A2.7:
///   - green  (upload arrow)      → < 80% (< 4 000 events)
///   - orange (warning triangle)  → 80–99% (4 000–4 999 events)
///   - red    (octagon xmark)     → 100%+ (≥ 5 000 events); new captures blocked
///
/// Council A11y-B2 (2026-05-13): status is conveyed by both color AND distinct
/// SF Symbol shape to satisfy WCAG 1.4.1 Use of Color.
///
/// Tap-to-force-sync calls `onForceSyncTapped`. The caller is responsible for
/// dispatching to `SyncEngine.drainNextBatch(limit:)`.
struct QueueStatusRow: View {
    let status: QueueStatusSnapshot
    let onForceSyncTapped: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            // Status icon — shape + color, hidden from VoiceOver (semantics
            // are carried by the text a11y label). Council A11y-B1: icon is
            // outside the `.combine` group, which now covers text-only.
            Image(systemName: statusIconName)
                .foregroundStyle(statusColor)
                .accessibilityHidden(true)
                .frame(width: 20)

            // Status text group — combined into a single VoiceOver element.
            // Council A11y-B1: .combine applied only here, not on the outer
            // HStack, so the Button below remains a separate focusable element.
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(eventCountLabel)
                        .font(.subheadline)
                        .foregroundStyle(statusColor)

                    if status.level != .ok {
                        Text(levelBadgeLabel)
                            .font(.caption2.weight(.semibold))
                            .padding(.horizontal, 6)
                            .padding(.vertical, 2)
                            .background(statusColor.opacity(0.15), in: Capsule())
                            .foregroundStyle(statusColor)
                    }
                }

                HStack(spacing: 8) {
                    Text(blobSizeLabel)
                        .font(.caption)
                        .foregroundStyle(.secondary)

                    Text("·")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .accessibilityHidden(true)

                    Text(lastSyncLabel)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            .accessibilityElement(children: .combine)
            .accessibilityLabel(combinedStatusAccessibilityLabel)

            Spacer()

            // Button is a separate VoiceOver element — must not be inside
            // the .combine group above. Council A11y-B1.
            Button("Sync now", action: onForceSyncTapped)
                .font(.caption)
                .buttonStyle(.bordered)
                .disabled(status.pendingCount == 0)
                .accessibilityHint(status.pendingCount == 0
                    ? "No pending events to sync"
                    : "Forces immediate upload of \(status.pendingCount) pending events")
        }
        .padding(.vertical, 8)
    }

    // MARK: - Computed display values

    private var statusIconName: String {
        switch status.level {
        case .ok:      return "arrow.up.circle.fill"
        case .warning: return "exclamationmark.triangle.fill"
        case .blocked: return "xmark.octagon.fill"
        }
    }

    private var statusColor: Color {
        switch status.level {
        case .ok:      return .green
        case .warning: return .orange   // orange (not yellow) for WCAG 1.4.3 contrast
        case .blocked: return .red
        }
    }

    private var levelBadgeLabel: String {
        switch status.level {
        case .ok:      return ""
        case .warning: return "Near limit"
        case .blocked: return "Blocked"
        }
    }

    private var eventCountLabel: String {
        switch status.pendingCount {
        case 0:       return "No events queued"
        case 1:       return "1 event queued"
        default:      return "\(status.pendingCount) events queued"
        }
    }

    private var combinedStatusAccessibilityLabel: String {
        let base = eventCountLabel
        let suffix: String
        switch status.level {
        case .ok:      suffix = ""
        case .warning: suffix = " — approaching capacity"
        case .blocked: suffix = " — queue full, captures blocked"
        }
        return "\(base)\(suffix), \(blobSizeLabel), \(lastSyncLabel)"
    }

    private var blobSizeLabel: String {
        let mb = Double(status.blobBytes) / 1_048_576
        return String(format: "%.1f MB", mb)
    }

    private var lastSyncLabel: String {
        guard let date = status.lastSyncDate else { return "Never synced" }
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .short
        return "Last sync \(formatter.localizedString(for: date, relativeTo: Date()))"
    }
}

// MARK: - QueueStatusSnapshot

/// Point-in-time UI summary of the outbound event queue.
/// Distinct from the `QueueStatus` persistence enum (DB column values).
struct QueueStatusSnapshot {
    let pendingCount: Int
    let blobBytes: Int64
    let lastSyncDate: Date?

    /// Per ADR 0028-A2.7: 80% = 4 000 events, 100% = 5 000 events.
    static let warningThreshold = 4_000
    static let blockedThreshold = 5_000

    enum Level { case ok, warning, blocked }

    var level: Level {
        switch pendingCount {
        case ..<QueueStatusSnapshot.warningThreshold: return .ok
        case ..<QueueStatusSnapshot.blockedThreshold: return .warning
        default:                                      return .blocked
        }
    }

    var isCapacityBlocked: Bool { level == .blocked }
}
