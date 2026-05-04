import Foundation
import Crypto

/// Content-addressed binary blob store backed by the app sandbox file system.
///
/// Each blob is stored at `<root>/<sha256-hex>.bin` where `<sha256-hex>` is
/// the lowercase hex SHA-256 of the bytes. Reference counting is implicit
/// (the SQLite `event_queue.blob_ref` column points at hashes); compaction
/// (Phase 2 cap rule) walks `event_queue` and deletes blob files whose hash
/// no longer appears in any pending row.
///
/// Per W#23 P2 hand-off + ADR 0028-A2.7 (5000-event / 500MB caps).
public struct BlobStore: Sendable {
    public let rootDirectory: URL

    public init(rootDirectory: URL) throws {
        try FileManager.default.createDirectory(at: rootDirectory, withIntermediateDirectories: true)
        self.rootDirectory = rootDirectory
    }

    /// Write `data` into the store and return its content address (lowercase
    /// hex SHA-256). Idempotent: writing the same bytes twice produces the
    /// same address and overwrites with identical content.
    @discardableResult
    public func put(_ data: Data) throws -> String {
        let hash = Self.contentAddress(of: data)
        let url = blobURL(for: hash)
        try data.write(to: url, options: [.atomic])
        return hash
    }

    /// Read the bytes at the supplied content address, or `nil` when the
    /// address is not present in the store.
    public func get(address: String) throws -> Data? {
        let url = blobURL(for: address)
        guard FileManager.default.fileExists(atPath: url.path) else { return nil }
        return try Data(contentsOf: url)
    }

    /// Returns true if a blob with the supplied content address is present.
    public func contains(address: String) -> Bool {
        FileManager.default.fileExists(atPath: blobURL(for: address).path)
    }

    /// Delete a blob by content address. Returns true if a blob was removed,
    /// false if no blob existed at that address.
    @discardableResult
    public func remove(address: String) throws -> Bool {
        let url = blobURL(for: address)
        guard FileManager.default.fileExists(atPath: url.path) else { return false }
        try FileManager.default.removeItem(at: url)
        return true
    }

    /// Total bytes consumed by every blob in the store. O(n) over the
    /// directory listing; callers should cache when used in hot paths.
    public func totalBytes() throws -> Int64 {
        let urls = try FileManager.default.contentsOfDirectory(at: rootDirectory, includingPropertiesForKeys: [.fileSizeKey])
        var total: Int64 = 0
        for url in urls {
            let resourceValues = try url.resourceValues(forKeys: [.fileSizeKey])
            if let size = resourceValues.fileSize {
                total += Int64(size)
            }
        }
        return total
    }

    private func blobURL(for address: String) -> URL {
        rootDirectory.appendingPathComponent("\(address).bin")
    }

    /// Compute the content-address of `data` (lowercase hex SHA-256).
    public static func contentAddress(of data: Data) -> String {
        let digest = SHA256.hash(data: data)
        return digest.map { String(format: "%02x", $0) }.joined()
    }
}
