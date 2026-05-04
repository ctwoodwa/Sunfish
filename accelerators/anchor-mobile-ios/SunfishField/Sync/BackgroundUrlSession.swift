import Foundation

/// Singleton background URLSession factory for the W#23 outbound sync
/// engine. Per ADR 0028-A2.2: one background session per app
/// (Apple-recommended pattern); identifier `dev.sunfish.field.upload`;
/// the listed configuration knobs are explicit so a future audit can
/// confirm they match the ADR.
public enum BackgroundUrlSession {
    public static let identifier = "dev.sunfish.field.upload"

    /// Build the canonical `URLSessionConfiguration.background` for
    /// outbound field-event + field-blob uploads.
    ///
    /// Per ADR 0028-A2.2 the explicit knobs are:
    /// - `discretionary = false` — uploads are user-visible work, not
    ///   background "best-effort"
    /// - `sessionSendsLaunchEvents = true` — wakes the app when the
    ///   session's tasks complete in the background
    /// - `allowsCellularAccess = true` — operators on cellular still get
    ///   sync (mobile field-capture context)
    /// - `waitsForConnectivity = true` — defer the request when the
    ///   device is offline rather than failing immediately
    /// - `timeoutIntervalForResource = 7 * 24 * 3600` — single-attempt
    ///   resource timeout extends across long offline windows
    public static func makeConfiguration() -> URLSessionConfiguration {
        let configuration = URLSessionConfiguration.background(withIdentifier: identifier)
        configuration.isDiscretionary = false
        configuration.sessionSendsLaunchEvents = true
        configuration.allowsCellularAccess = true
        configuration.waitsForConnectivity = true
        configuration.timeoutIntervalForResource = 7 * 24 * 3600
        return configuration
    }

    /// Create the canonical session. Caller supplies the
    /// `URLSessionDelegate` (typically `SyncDelegate`); session retention
    /// is the caller's responsibility (one-per-app per ADR 0028-A2.2).
    public static func makeSession(delegate: URLSessionDelegate) -> URLSession {
        URLSession(
            configuration: makeConfiguration(),
            delegate: delegate,
            delegateQueue: nil)
    }
}
