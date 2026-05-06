namespace Sunfish.Foundation.SickBay;

/// <summary>
/// k-anonymity-aware count for Sick Bay pharmacy records per ADR 0082
/// §1 + §Trust impact. Counts below the k=3 floor are SUPPRESSED — the
/// browse pane never reveals exact counts of 1 or 2 because doing so
/// permits inference about a single individual's recovery / medical
/// keymaterial state.
/// </summary>
/// <remarks>
/// Use the static factory <see cref="Exact(int)"/> rather than calling
/// the private constructor directly. <see cref="Suppressed"/> is the
/// canonical "below k" sentinel.
/// </remarks>
public sealed record PharmacyRecordCount
{
    /// <summary>k-anonymity floor per ADR 0082 §Trust impact.</summary>
    public const int KAnonymityFloor = 3;

    /// <summary>Singleton "below-floor" instance; <see cref="Value"/> is null.</summary>
    public static readonly PharmacyRecordCount Suppressed = new(value: null);

    private PharmacyRecordCount(int? value) { Value = value; }

    /// <summary>The exact count when ≥ k-floor; null when suppressed.</summary>
    public int? Value { get; }

    /// <summary>True when this instance is the suppressed "below-floor" sentinel.</summary>
    public bool IsSuppressed => Value is null;

    /// <summary>
    /// Construct a count, returning <see cref="Suppressed"/> when
    /// <paramref name="count"/> is below <see cref="KAnonymityFloor"/>.
    /// Negative counts are treated as zero per defensive-input cohort
    /// convention. Per W#54 P1 council Minor m3: suppression is
    /// §Trust-safe — coercing a malformed (e.g., negative) count to
    /// <see cref="Suppressed"/> CANNOT under-anonymize, so silent
    /// coercion is the right failure mode for an upstream-bug input.
    /// </summary>
    public static PharmacyRecordCount Exact(int count)
    {
        var clamped = count < 0 ? 0 : count;
        return clamped < KAnonymityFloor
            ? Suppressed
            : new PharmacyRecordCount(clamped);
    }
}
