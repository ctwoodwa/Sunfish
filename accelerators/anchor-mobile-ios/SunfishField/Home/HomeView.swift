import SwiftUI
import SunfishFieldIdentity

/// Main home scene shown after the device is paired. Per W#23 Phase 6
/// (ADR 0028-A2.7 queue-status UX).
///
/// Capture-flow entry points are placeholder tiles — each follow-up hand-off
/// (W#23.1–W#23.6) replaces the relevant tile with a live navigation link.
struct HomeView: View {
    @StateObject private var viewModel: HomeViewModel

    init(
        queueService: any EventQueueServicing,
        syncEngine: SyncEngine,
        onUnpaired: @escaping @MainActor () -> Void = {}
    ) {
        _viewModel = StateObject(wrappedValue: HomeViewModel(
            queueService: queueService,
            syncEngine: syncEngine,
            onUnpaired: onUnpaired))
    }

    var body: some View {
        NavigationStack {
            List {
                Section("Capture") {
                    ForEach(CaptureFlow.allCases) { flow in
                        Label(flow.label, systemImage: flow.icon)
                            .foregroundStyle(.secondary)
                            .accessibilityLabel("\(flow.label) — coming soon")
                    }
                }

                Section {
                    QueueStatusRow(
                        status: viewModel.queueStatus,
                        onForceSyncTapped: { Task { await viewModel.forceSyncAsync() } })
                } header: {
                    Text("Sync queue")
                }
            }
            .navigationTitle("Sunfish Field")
            .toolbar {
                ToolbarItem(placement: .automatic) {
                    NavigationLink(destination: SettingsView(onUnpaired: viewModel.handleUnpaired)) {
                        Label("Settings", systemImage: "gear")
                    }
                }
            }
            .task { await viewModel.refreshAsync() }
            .refreshable { await viewModel.refreshAsync() }
            .alert("Sync error", isPresented: $viewModel.showSyncError) {
                Button("OK", role: .cancel) {}
            } message: {
                Text(viewModel.syncErrorMessage)
            }
        }
    }
}

// MARK: - CaptureFlow

private enum CaptureFlow: String, CaseIterable, Identifiable {
    case receipt, asset, inspection, signature, mileage, workOrder

    var id: String { rawValue }

    var label: String {
        switch self {
        case .receipt:    return "Receipt"
        case .asset:      return "Asset"
        case .inspection: return "Inspection"
        case .signature:  return "Signature"
        case .mileage:    return "Mileage"
        case .workOrder:  return "Work Order"
        }
    }

    var icon: String {
        switch self {
        case .receipt:    return "doc.text"
        case .asset:      return "wrench"
        case .inspection: return "checklist"
        case .signature:  return "signature"
        case .mileage:    return "car"
        case .workOrder:  return "hammer"
        }
    }
}

// MARK: - HomeViewModel

@MainActor
final class HomeViewModel: ObservableObject {
    @Published var queueStatus = QueueStatusSnapshot(pendingCount: 0, blobBytes: 0, lastSyncDate: nil)
    @Published var showSyncError = false
    @Published private(set) var syncErrorMessage = ""

    private let queueService: any EventQueueServicing
    private let syncEngine: SyncEngine
    private let onUnpaired: @MainActor () -> Void

    init(
        queueService: any EventQueueServicing,
        syncEngine: SyncEngine,
        onUnpaired: @escaping @MainActor () -> Void
    ) {
        self.queueService = queueService
        self.syncEngine = syncEngine
        self.onUnpaired = onUnpaired
    }

    var handleUnpaired: @MainActor () -> Void { onUnpaired }

    func refreshAsync() async {
        do {
            let count = try await queueService.pendingCount()
            let bytes = try await queueService.pendingBlobBytes()
            queueStatus = QueueStatusSnapshot(
                pendingCount: count,
                blobBytes: bytes,
                lastSyncDate: queueStatus.lastSyncDate)
        } catch {
            // Non-fatal: stale status is better than crashing.
        }
    }

    func forceSyncAsync() async {
        do {
            _ = try await syncEngine.drainNextBatch(limit: 50)
            await refreshAsync()
        } catch {
            syncErrorMessage = error.localizedDescription
            showSyncError = true
        }
    }
}
