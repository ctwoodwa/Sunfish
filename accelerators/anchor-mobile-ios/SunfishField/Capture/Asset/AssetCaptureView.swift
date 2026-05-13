import SwiftUI

/// Lightweight display-only projection of an equipment record for v1.
/// The full equipment list is fetched via GET /api/v1/equipment in a
/// deepening follow-up; v1 seeds one stub item from ContentView.
public struct EquipmentListItem: Identifiable, Sendable, Equatable {
    public let id: String
    public let name: String

    public init(id: String, name: String) {
        self.id = id
        self.name = name
    }
}

/// Camera capture view for `EventType.Asset` envelopes. Presents the
/// system camera, stores the JPEG via BlobStore, and enqueues an
/// EventEnvelope for upload via the existing SyncEngine background loop.
struct AssetCaptureView: View {
    let equipment: EquipmentListItem
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32

    @State private var queuedBlobRef: String?
    @State private var errorMessage: String?
    @State private var showPicker = false

    init(
        equipment: EquipmentListItem,
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        deviceId: String,
        capturedUnderKernel: String = "1.0.0",
        capturedUnderSchemaEpoch: UInt32 = 1
    ) {
        self.equipment = equipment
        self.queueService = queueService
        self.blobStore = blobStore
        self.deviceId = deviceId
        self.capturedUnderKernel = capturedUnderKernel
        self.capturedUnderSchemaEpoch = capturedUnderSchemaEpoch
    }

    var body: some View {
        VStack(spacing: 20) {
            Text(equipment.name)
                .font(.headline)
                .accessibilityAddTraits(.isHeader)

#if os(iOS)
            if queuedBlobRef != nil {
                Label("Photo queued for upload", systemImage: "checkmark.circle.fill")
                    .foregroundStyle(.green)
            } else {
                Button {
                    showPicker = true
                } label: {
                    Label("Capture photo", systemImage: "camera")
                }
                .buttonStyle(.borderedProminent)
                .sheet(isPresented: $showPicker) {
                    CameraPickerView { image in
                        Task { await enqueue(image: image) }
                    }
                }
            }
#else
            Text("Camera capture requires iOS.")
                .foregroundStyle(.secondary)
#endif

            if let error = errorMessage {
                Label(error, systemImage: "exclamationmark.triangle")
                    .foregroundStyle(.red)
                    .accessibilityLabel("Error: \(error)")
            }
        }
        .padding()
        .navigationTitle("Asset Photo")
    }

#if os(iOS)
    @MainActor
    private func enqueue(image: UIImage) async {
        // Council AMENDMENT-3: strip EXIF/GPS before storing.
        guard let jpegData = strippedJpegData(from: image, quality: 0.85) else {
            errorMessage = "Could not compress photo. Please try again."
            return
        }
        do {
            let blobRef = try blobStore.put(jpegData)
            let payloadData = try JsonCanonical.serialize(
                AssetCapturePayload(equipmentId: equipment.id))
            // Council AMENDMENT-1: monotonic seq via actor-backed generator.
            let seq = await AssetCaptureView.seqGenerator.next()
            let envelope = EventEnvelope(
                deviceLocalSeq: seq,
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .Asset,
                payload: payloadData,
                blobRef: blobRef,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch)
            try await queueService.appendAsync(envelope: envelope)
            queuedBlobRef = blobRef
            showPicker = false
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    /// AMENDMENT-3: re-encode JPEG through CGImageDestination stripping
    /// EXIF, GPS, and TIFF metadata to prevent silent location leakage.
    private func strippedJpegData(from image: UIImage, quality: CGFloat) -> Data? {
        guard let cgImage = image.cgImage else { return nil }
        let data = NSMutableData()
        guard let dest = CGImageDestinationCreateWithData(
            data, "public.jpeg" as CFString, 1, nil) else { return nil }
        let options: [CFString: Any] = [
            kCGImageDestinationLossyCompressionQuality: quality,
            kCGImagePropertyExifDictionary: [:] as [String: Any],
            kCGImagePropertyGPSDictionary:  [:] as [String: Any],
            kCGImagePropertyTIFFDictionary: [:] as [String: Any],
        ]
        CGImageDestinationAddImage(dest, cgImage, options as CFDictionary)
        guard CGImageDestinationFinalize(dest) else { return nil }
        return data as Data
    }
#endif

    /// AMENDMENT-1: process-scoped monotonic sequence-number generator.
    /// max(candidate, last+1) ensures strict monotonicity even through
    /// NTP clock-step-back. GRDB-backed persistence (for cross-launch
    /// safety) is P2 scope.
    private static let seqGenerator = DeviceLocalSeqGenerator()
}

// MARK: - DeviceLocalSeqGenerator (AMENDMENT-1)

private actor DeviceLocalSeqGenerator {
    private var lastIssued: UInt64 = 0

    func next() -> UInt64 {
        let candidate = UInt64(Date().timeIntervalSince1970 * 1_000_000)
        let next = max(candidate, lastIssued &+ 1)
        lastIssued = next
        return next
    }
}

// MARK: - CameraPickerView (iOS only)

#if os(iOS)
import UIKit
import ImageIO

/// Wraps `UIImagePickerController` in a SwiftUI-compatible sheet.
private struct CameraPickerView: UIViewControllerRepresentable {
    let onImagePicked: (UIImage) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(onImagePicked: onImagePicked)
    }

    func makeUIViewController(context: Context) -> UIImagePickerController {
        let picker = UIImagePickerController()
#if targetEnvironment(simulator)
        // Simulator has no camera; allow photo library for smoke testing.
        picker.sourceType = UIImagePickerController.isSourceTypeAvailable(.camera)
            ? .camera : .photoLibrary
#else
        // Device builds: camera only — no library fallback (AMENDMENT council INFO-6).
        picker.sourceType = .camera
#endif
        picker.delegate = context.coordinator
        return picker
    }

    func updateUIViewController(_ uiViewController: UIImagePickerController, context: Context) {}

    final class Coordinator: NSObject,
        UIImagePickerControllerDelegate, UINavigationControllerDelegate {
        let onImagePicked: (UIImage) -> Void

        init(onImagePicked: @escaping (UIImage) -> Void) {
            self.onImagePicked = onImagePicked
        }

        func imagePickerController(
            _ picker: UIImagePickerController,
            didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]
        ) {
            if let image = info[.originalImage] as? UIImage {
                onImagePicked(image)
            }
            picker.dismiss(animated: true)
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            picker.dismiss(animated: true)
        }
    }
}
#endif
