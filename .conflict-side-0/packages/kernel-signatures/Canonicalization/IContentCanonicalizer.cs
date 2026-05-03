namespace Sunfish.Kernel.Signatures.Canonicalization;

/// <summary>
/// Produces canonical bytes for the document being signed (per ADR 0054
/// amendment A1). Implementations cover specific document classes:
/// JSON via RFC 8785 (approximated by <c>Foundation.Crypto.CanonicalJson</c>),
/// plain text via UTF-8 NFC, and PDF via PDF/A. The chosen canonicalization
/// is pinned at the substrate boundary so signature verification stays
/// stable across input-formatter changes.
/// </summary>
public interface IContentCanonicalizer
{
    /// <summary>Identifier for this canonicalizer (audit-record provenance).</summary>
    string CanonicalizationKind { get; }

    /// <summary>Produces canonical bytes for hashing.</summary>
    byte[] Canonicalize(object content);
}
