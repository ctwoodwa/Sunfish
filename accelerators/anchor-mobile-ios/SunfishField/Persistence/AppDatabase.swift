import Foundation
import GRDB

/// Local-first persistence layer for the field-capture app.
///
/// Wraps GRDB's `DatabaseQueue` against a file-backed SQLite database in the
/// app sandbox at `Library/Application Support/SunfishField/database.sqlite`.
///
/// **Encryption status (Phase 2 substrate v1):** plaintext SQLite + iOS file-
/// system encryption via `NSFileProtectionComplete` (the file is unreadable
/// while the device is locked; readable once unlocked). SQLCipher whole-
/// database encryption is deferred to a follow-up phase pending the SPM-
/// packaging story for SQLCipher (the standard `groue/GRDB.swift` package
/// targets system SQLite which does not include SQLCipher patches; switching
/// requires either a fork or a separate SPM-packaged SQLCipher.swift, both
/// of which have unsettled licensing + maintenance trade-offs at the
/// `pre-release-latest-first` policy stage). Per ADR 0028-A2.3 the encryption
/// is required for shipping; substrate v1 documents the gap as a halt-
/// condition deferral with TODO.
public final class AppDatabase: @unchecked Sendable {
    /// File-system path to the database under the app sandbox.
    public let databasePath: URL

    /// Underlying GRDB queue. All reads + writes funnel through here.
    public let queue: DatabaseQueue

    /// Open (or create) the database at the standard sandbox location and
    /// run any pending schema migrations. The caller owns the returned
    /// instance for the lifetime of the app.
    public static func open(at directory: URL) throws -> AppDatabase {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let url = directory.appendingPathComponent("database.sqlite")
        var configuration = Configuration()
        configuration.foreignKeysEnabled = true
        configuration.label = "SunfishField"
        let queue = try DatabaseQueue(path: url.path, configuration: configuration)
        try queue.write { db in
            // TODO(SQLCipher): apply the SQLCipher key here once the SPM
            // packaging is settled. For substrate v1 the database is
            // plaintext under file-system protection only.
            _ = db
        }
        let database = AppDatabase(databasePath: url, queue: queue)
        try database.applyMigrations()
        try database.applyDataProtection()
        return database
    }

    private init(databasePath: URL, queue: DatabaseQueue) {
        self.databasePath = databasePath
        self.queue = queue
    }

    private func applyMigrations() throws {
        var migrator = DatabaseMigrator()
        V1Migration.register(in: &migrator)
        try migrator.migrate(queue)
    }

    /// Mark the database file as Complete-protection (per ADR 0028-A2.3).
    /// Background URLSession reads need to consult the database while the
    /// device is locked — Phase 4 will need to negotiate the protection
    /// class against background-task constraints; substrate v1 ships
    /// Complete and documents the open question.
    private func applyDataProtection() throws {
        #if canImport(Foundation) && os(iOS)
        let attributes: [FileAttributeKey: Any] = [
            .protectionKey: FileProtectionType.complete,
        ]
        try FileManager.default.setAttributes(attributes, ofItemAtPath: databasePath.path)
        #endif
    }
}
