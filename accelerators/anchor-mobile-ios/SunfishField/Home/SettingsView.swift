import SwiftUI
import SunfishFieldIdentity

/// Minimal settings sheet: device identity, paired tenant, unpair action,
/// and sync history. Per W#23 Phase 6 hand-off.
struct SettingsView: View {
    /// Called after a successful unpair so the root scene can navigate back
    /// to the pairing flow.
    let onUnpaired: () -> Void

    @StateObject private var viewModel = SettingsViewModel()
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            Form {
                deviceSection
                syncHistorySection
                dangerSection
            }
            .navigationTitle("Settings")
            .toolbar {
                ToolbarItem(placement: .automatic) {
                    Button("Done") { dismiss() }
                }
            }
            .alert("Unpair this device?", isPresented: $viewModel.showUnpairConfirmation) {
                Button("Unpair", role: .destructive) {
                    Task { await viewModel.unpairAsync(then: handleUnpaired) }
                }
                Button("Cancel", role: .cancel) {}
            } message: {
                Text("The device will be disconnected from Anchor. Local queue data is preserved but sync will stop until re-pairing.")
            }
            .alert("Unpair failed", isPresented: $viewModel.showUnpairError) {
                Button("OK", role: .cancel) {}
            } message: {
                Text(viewModel.unpairErrorMessage)
            }
        }
    }

    // MARK: Sections

    private var deviceSection: some View {
        Section("Device") {
            LabeledContent("Device ID") {
                Text(viewModel.deviceId)
                    .font(.system(.caption, design: .monospaced))
                    .textSelection(.enabled)
                    .foregroundStyle(.secondary)
            }
            .accessibilityLabel("Device ID: \(viewModel.deviceId)")

            LabeledContent("Paired tenant") {
                Text(viewModel.pairedTenant)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private var syncHistorySection: some View {
        Section("Sync history") {
            if viewModel.syncHistory.isEmpty {
                Text("No sync attempts recorded")
                    .foregroundStyle(.secondary)
            } else {
                ForEach(viewModel.syncHistory) { entry in
                    VStack(alignment: .leading, spacing: 2) {
                        HStack {
                            Image(systemName: entry.succeeded ? "checkmark.circle.fill" : "xmark.circle.fill")
                                .foregroundStyle(entry.succeeded ? .green : .red)
                                .accessibilityHidden(true)
                            Text(entry.relativeDate)
                                .font(.subheadline)
                        }
                        if !entry.detail.isEmpty {
                            Text(entry.detail)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                    }
                    .accessibilityLabel("\(entry.succeeded ? "Succeeded" : "Failed") — \(entry.relativeDate)\(entry.detail.isEmpty ? "" : ": \(entry.detail)")")
                }
            }
        }
    }

    private var dangerSection: some View {
        Section {
            Button(role: .destructive) {
                viewModel.showUnpairConfirmation = true
            } label: {
                Label("Unpair this device", systemImage: "link.badge.minus")
            }
        }
    }

    // MARK: Helpers

    private func handleUnpaired() {
        dismiss()
        onUnpaired()
    }
}

// MARK: - SyncHistoryEntry

struct SyncHistoryEntry: Identifiable {
    let id: UUID
    let date: Date
    let succeeded: Bool
    let detail: String

    var relativeDate: String {
        let f = RelativeDateTimeFormatter()
        f.unitsStyle = .short
        return f.localizedString(for: date, relativeTo: Date())
    }
}

// MARK: - SettingsViewModel

@MainActor
final class SettingsViewModel: ObservableObject {
    @Published var deviceId = "—"
    @Published var pairedTenant = "—"
    @Published var syncHistory: [SyncHistoryEntry] = []
    @Published var showUnpairConfirmation = false
    @Published var showUnpairError = false
    @Published private(set) var unpairErrorMessage = ""

    private var bridgeBaseURL: URL = URL(string: "https://bridge.local")!

    init() {
        loadDeviceId()
    }

    private func loadDeviceId() {
        if let identity = try? InstallIdentityKeychain.load() {
            deviceId = identity.deviceId.value
        }
    }

    func unpairAsync(then completion: @escaping () -> Void) async {
        do {
            try await postUnpair()
            try? InstallIdentityKeychain.clear()
            completion()
        } catch {
            unpairErrorMessage = error.localizedDescription
            showUnpairError = true
        }
    }

    private func postUnpair() async throws {
        let url = bridgeBaseURL.appendingPathComponent("/api/v1/field/unpair")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.timeoutInterval = 15
        let (_, response) = try await URLSession.shared.data(for: request)
        guard let http = response as? HTTPURLResponse,
              (200..<300).contains(http.statusCode) else {
            throw UnpairError.serverRejected
        }
    }
}

private enum UnpairError: LocalizedError {
    case serverRejected

    var errorDescription: String? {
        "The server rejected the unpair request. Check your network and try again."
    }
}

// MARK: - Keychain stubs (Phase 5 wires these up fully)

private enum InstallIdentityKeychain {
    /// Load the install identity from the Keychain.
    /// Phase 5 replaces this stub with the real `InstallIdentity+Keychain` read path.
    static func load() throws -> InstallIdentity? { nil }

    /// Remove all Keychain entries for the install identity on unpair.
    /// Phase 5 replaces this stub with the real `SecItemDelete` clear path.
    static func clear() throws {}
}
