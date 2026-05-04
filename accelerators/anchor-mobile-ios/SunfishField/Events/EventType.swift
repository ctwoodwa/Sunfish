import Foundation

/// Capture domain enum per ADR 0028-A2.1 (per-event-type LWW table). Each
/// raw value names one of the six field-capture domains. The enum is
/// `Codable` + `String`-backed so it round-trips through canonical JSON
/// without integer-vs-string ambiguity (cohort precedent: ADR 0028 §A7.8).
public enum EventType: String, Codable, Sendable, CaseIterable {
    /// Receipt capture — photo + OCR + categorize.
    case Receipt
    /// Asset / equipment capture — nameplate OCR + barcode + condition.
    case Asset
    /// Inspection — structured form + photos + condition assessments.
    case Inspection
    /// Signature — PencilKit canvas + CryptoKit signing + PDF.
    case Signature
    /// Mileage — manual entry + odometer + property-link.
    case Mileage
    /// Work-Order Response — open-from-finding + status updates.
    case WorkOrderResponse
}
