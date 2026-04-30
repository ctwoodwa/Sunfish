using System.Text;

namespace Sunfish.Kernel.Signatures.Canonicalization;

/// <summary>
/// Plain-text canonicalizer applying Unicode Normalization Form C (NFC)
/// + UTF-8 byte encoding. ADR 0054 amendment A1 pins this rule for
/// non-structured plain-text documents.
/// </summary>
public sealed class Utf8NfcCanonicalizer : IContentCanonicalizer
{
    /// <inheritdoc />
    public string CanonicalizationKind => "utf-8-nfc";

    /// <inheritdoc />
    public byte[] Canonicalize(object content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content is not string text)
        {
            throw new ArgumentException(
                $"Utf8NfcCanonicalizer expects a string; got {content.GetType().FullName}.",
                nameof(content));
        }
        return Encoding.UTF8.GetBytes(text.Normalize(NormalizationForm.FormC));
    }
}
