using Sunfish.Tooling.ColorAudit;
using Xunit;

namespace Sunfish.Tooling.ColorAudit.Tests;

/// <summary>
/// Reference test vectors for CIEDE2000 from Sharma, Wu, Dalal supplementary table 1
/// ("The CIEDE2000 Color-Difference Formula", 2005). Confirms our implementation
/// matches published values within 1e-3.
/// </summary>
public class DeltaE2000Tests
{
    [Theory]
    // Format: L1, A1, B1, L2, A2, B2, expected ΔE2000
    [InlineData(50.0000, 2.6772, -79.7751,  50.0000, 0.0000, -82.7485, 2.0425)]
    [InlineData(50.0000, 3.1571, -77.2803,  50.0000, 0.0000, -82.7485, 2.8615)]
    [InlineData(50.0000, 2.8361, -74.0200,  50.0000, 0.0000, -82.7485, 3.4412)]
    [InlineData(50.0000, -1.3802, -84.2814, 50.0000, 0.0000, -82.7485, 1.0000)]
    [InlineData(50.0000, -1.1848, -84.8006, 50.0000, 0.0000, -82.7485, 1.0000)]
    [InlineData(50.0000, -0.9009, -85.5211, 50.0000, 0.0000, -82.7485, 1.0000)]
    [InlineData(50.0000, 0.0000, 0.0000,    50.0000, -1.0000, 2.0000,  2.3669)]
    [InlineData(50.0000, -1.0000, 2.0000,   50.0000, 0.0000, 0.0000,   2.3669)]
    [InlineData(60.2574, -34.0099, 36.2677, 60.4626, -34.1751, 39.4387, 1.2644)]
    [InlineData(63.0109, -31.0961, -5.8663, 62.8187, -29.7946, -4.0864, 1.2630)]
    [InlineData(50.0000, 2.5000, 0.0000,    50.0000, 0.0000, -2.5000,  4.3065)]
    public void Cie2000_MatchesSharmaWuDalalReferenceVectors(
        double l1, double a1, double b1, double l2, double a2, double b2, double expected)
    {
        var lab1 = new CieLab(l1, a1, b1);
        var lab2 = new CieLab(l2, a2, b2);
        var actual = DeltaE.Cie2000(lab1, lab2);
        Assert.Equal(expected, actual, precision: 3);
    }

    [Fact]
    public void Cie2000_IdenticalColors_ReturnsZero()
    {
        Assert.Equal(0.0, DeltaE.Cie2000("#27ae60", "#27ae60"), precision: 6);
    }

    [Fact]
    public void Cie2000_BlackWhite_IsLargest()
    {
        var d = DeltaE.Cie2000("#000000", "#ffffff");
        Assert.True(d > 99.0, $"Black vs white ΔE2000 = {d}; expected ~100.");
    }

    [Fact]
    public void LinearRgb_HexRoundTrip_PreservesValueWithinRoundoff()
    {
        var rgb = LinearRgb.FromSrgbHex("#27ae60");
        var hex = rgb.ToSrgbHex();
        Assert.Equal("#27AE60", hex);
    }
}
