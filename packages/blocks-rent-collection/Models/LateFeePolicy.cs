namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>
/// Passive record describing how late fees are assessed when rent is overdue.
/// This is a data-only record in this pass — no workflow automation is implemented.
/// Late-fee calculation and application are deferred to a follow-up.
/// </summary>
public sealed record LateFeePolicy
{
    /// <summary>
    /// Initialises a <see cref="LateFeePolicy"/>.
    /// </summary>
    /// <param name="Id">Unique policy identifier.</param>
    /// <param name="GracePeriodDays">
    /// Number of calendar days after the due date before a late fee applies.
    /// </param>
    /// <param name="FlatFee">
    /// Optional fixed fee charged when rent is overdue.
    /// At least one of <paramref name="FlatFee"/> or <paramref name="PercentageFee"/> must be set.
    /// <para><b>Precision note:</b> stored as <see cref="decimal"/> with a two-decimal-place
    /// assumption. Rounding enforcement is deferred to a follow-up.</para>
    /// </param>
    /// <param name="PercentageFee">
    /// Optional percentage of the overdue amount charged as a fee (0–100).
    /// At least one of <paramref name="FlatFee"/> or <paramref name="PercentageFee"/> must be set.
    /// A policy may specify both, in which case both fees are applied.
    /// <para><b>Precision note:</b> stored as <see cref="decimal"/> with a two-decimal-place
    /// assumption. Rounding enforcement is deferred to a follow-up.</para>
    /// </param>
    /// <param name="CapAmount">
    /// Optional ceiling on the total late fee charged per occurrence.
    /// <para><b>Precision note:</b> stored as <see cref="decimal"/> with a two-decimal-place
    /// assumption. Rounding enforcement is deferred to a follow-up.</para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when neither <paramref name="FlatFee"/> nor <paramref name="PercentageFee"/> is provided.
    /// </exception>
    public LateFeePolicy(
        LateFeePolicyId Id,
        int GracePeriodDays,
        decimal? FlatFee,
        decimal? PercentageFee,
        decimal? CapAmount)
    {
        if (FlatFee is null && PercentageFee is null)
            throw new ArgumentException(
                "A LateFeePolicy must specify at least one of FlatFee or PercentageFee.");

        this.Id = Id;
        this.GracePeriodDays = GracePeriodDays;
        this.FlatFee = FlatFee;
        this.PercentageFee = PercentageFee;
        this.CapAmount = CapAmount;
    }

    /// <summary>Unique policy identifier.</summary>
    public LateFeePolicyId Id { get; init; }

    /// <summary>Number of calendar days after the due date before a late fee applies.</summary>
    public int GracePeriodDays { get; init; }

    /// <summary>
    /// Optional fixed late fee.
    /// <para><b>Precision note:</b> two-decimal-place assumption; rounding enforcement deferred.</para>
    /// </summary>
    public decimal? FlatFee { get; init; }

    /// <summary>
    /// Optional percentage fee (0–100).
    /// <para><b>Precision note:</b> two-decimal-place assumption; rounding enforcement deferred.</para>
    /// </summary>
    public decimal? PercentageFee { get; init; }

    /// <summary>
    /// Optional cap on the total fee per occurrence.
    /// <para><b>Precision note:</b> two-decimal-place assumption; rounding enforcement deferred.</para>
    /// </summary>
    public decimal? CapAmount { get; init; }
}
