namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// A validated tax year value. Must be in the range 2020–2100.
/// </summary>
public readonly record struct TaxYear
{
    /// <summary>Minimum supported tax year.</summary>
    public const int MinYear = 2020;

    /// <summary>Maximum supported tax year.</summary>
    public const int MaxYear = 2100;

    /// <summary>The calendar year integer, e.g. <c>2024</c>.</summary>
    public int Value { get; }

    /// <summary>
    /// Initializes a <see cref="TaxYear"/>, throwing <see cref="ArgumentOutOfRangeException"/>
    /// if <paramref name="value"/> is outside [2020, 2100].
    /// </summary>
    public TaxYear(int value)
    {
        if (value < MinYear || value > MaxYear)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"TaxYear must be between {MinYear} and {MaxYear} (got {value}).");
        Value = value;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
