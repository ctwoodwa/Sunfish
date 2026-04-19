namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// A single recipient row on a Form 1099-NEC (Nonemployee Compensation).
/// </summary>
/// <param name="RecipientName">Full legal name of the payee.</param>
/// <param name="RecipientTaxId">
/// Masked taxpayer identification number in format <c>"XXX-XX-1234"</c> (SSN)
/// or <c>"XX-XXX1234"</c> (EIN). Only the last 4 digits are unmasked.
/// </param>
/// <param name="RecipientAddress">Full mailing address of the payee.</param>
/// <param name="TotalPaid">
/// Total nonemployee compensation paid during the tax year.
/// IRS reporting threshold is $600; see <see cref="Validate"/>.
/// </param>
/// <param name="AccountNumber">Optional account/reference number assigned by the payer.</param>
public sealed record Nec1099Recipient(
    string RecipientName,
    string RecipientTaxId,
    string RecipientAddress,
    decimal TotalPaid,
    string? AccountNumber = null)
{
    /// <summary>
    /// IRS threshold below which a 1099-NEC is not required to be filed.
    /// Consumers decide whether to drop or flag below-threshold rows.
    /// </summary>
    public const decimal IrsThreshold = 600m;

    /// <summary>
    /// Returns <see langword="true"/> if this recipient meets the IRS $600 filing threshold.
    /// </summary>
    public bool MeetsThreshold => TotalPaid >= IrsThreshold;

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if <see cref="TotalPaid"/> is below the
    /// IRS $600 threshold. Consumers may call this to enforce server-side validation, or may
    /// use <see cref="MeetsThreshold"/> to filter/flag rows instead.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="TotalPaid"/> &lt; $600.</exception>
    public void Validate()
    {
        if (!MeetsThreshold)
            throw new InvalidOperationException(
                $"Recipient '{RecipientName}' was paid {TotalPaid:C}, which is below the IRS " +
                $"1099-NEC reporting threshold of {IrsThreshold:C}. Drop or flag this row before filing.");
    }
}
