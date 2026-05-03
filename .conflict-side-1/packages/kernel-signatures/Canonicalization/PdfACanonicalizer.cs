namespace Sunfish.Kernel.Signatures.Canonicalization;

/// <summary>
/// PDF/A canonicalizer — Phase 2 stub. Per ADR 0054 amendment A1, PDF
/// documents canonicalize to PDF/A bytes (deterministic xref + no
/// creation timestamps + embedded fonts). The actual PDF/A rendering
/// pipeline lives downstream of kernel-signatures (it requires a PDF
/// rendering library + font-embedding infrastructure that's outside
/// the kernel); this stub names the integration point + throws clearly
/// when invoked.
/// </summary>
/// <remarks>
/// When the downstream PDF rendering ships, the implementation lives
/// in a sibling package (e.g. <c>compat-pdf</c> or
/// <c>providers-pdfa-renderer</c>) and registers as the
/// <see cref="IContentCanonicalizer"/> instance for PDF inputs.
/// </remarks>
public sealed class PdfACanonicalizer : IContentCanonicalizer
{
    /// <inheritdoc />
    public string CanonicalizationKind => "pdf-a-1b";

    /// <inheritdoc />
    public byte[] Canonicalize(object content) =>
        throw new NotImplementedException(
            "PDF/A canonicalization is deferred to a downstream rendering pipeline. " +
            "Register a PDF/A-rendering implementation of IContentCanonicalizer + bind " +
            "it for PDF inputs at the host layer. See ADR 0054 amendment A1.");
}
