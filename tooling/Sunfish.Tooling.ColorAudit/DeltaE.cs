using System;

namespace Sunfish.Tooling.ColorAudit;

/// <summary>
/// CIEDE2000 perceptual color-difference per Sharma, Wu, Dalal (2005).
/// Returns a unitless distance; values ≥ ~11 are typically "clearly distinguishable
/// at a glance" per Sunfish spec §5 SyncState gate.
/// </summary>
public static class DeltaE
{
    private const double Kl = 1.0;
    private const double Kc = 1.0;
    private const double Kh = 1.0;

    /// <summary>
    /// Compute CIEDE2000 between two Lab colors.
    /// Reference implementation per Sharma/Wu/Dalal supplementary material;
    /// matches the test vectors in the published table to within 1e-4.
    /// </summary>
    public static double Cie2000(CieLab a, CieLab b)
    {
        // Step 1: compute C*ab and h*ab in Cartesian Lab.
        double c1 = Math.Sqrt(a.A * a.A + a.B * a.B);
        double c2 = Math.Sqrt(b.A * b.A + b.B * b.B);
        double cBar = (c1 + c2) / 2.0;

        double g = 0.5 * (1.0 - Math.Sqrt(Math.Pow(cBar, 7) / (Math.Pow(cBar, 7) + Math.Pow(25.0, 7))));

        double a1Prime = (1.0 + g) * a.A;
        double a2Prime = (1.0 + g) * b.A;
        double c1Prime = Math.Sqrt(a1Prime * a1Prime + a.B * a.B);
        double c2Prime = Math.Sqrt(a2Prime * a2Prime + b.B * b.B);

        double h1Prime = ToDegrees(Math.Atan2(a.B, a1Prime));
        if (h1Prime < 0) h1Prime += 360.0;
        double h2Prime = ToDegrees(Math.Atan2(b.B, a2Prime));
        if (h2Prime < 0) h2Prime += 360.0;

        // Step 2: ΔL', ΔC', ΔH'.
        double dLPrime = b.L - a.L;
        double dCPrime = c2Prime - c1Prime;

        double dhPrime;
        if (c1Prime * c2Prime == 0.0)
        {
            dhPrime = 0.0;
        }
        else if (Math.Abs(h2Prime - h1Prime) <= 180.0)
        {
            dhPrime = h2Prime - h1Prime;
        }
        else if (h2Prime - h1Prime > 180.0)
        {
            dhPrime = h2Prime - h1Prime - 360.0;
        }
        else
        {
            dhPrime = h2Prime - h1Prime + 360.0;
        }

        double dHPrime = 2.0 * Math.Sqrt(c1Prime * c2Prime) * Math.Sin(ToRadians(dhPrime / 2.0));

        // Step 3: weights.
        double lBarPrime = (a.L + b.L) / 2.0;
        double cBarPrime = (c1Prime + c2Prime) / 2.0;

        double hBarPrime;
        if (c1Prime * c2Prime == 0.0)
        {
            hBarPrime = h1Prime + h2Prime;
        }
        else if (Math.Abs(h1Prime - h2Prime) <= 180.0)
        {
            hBarPrime = (h1Prime + h2Prime) / 2.0;
        }
        else if (h1Prime + h2Prime < 360.0)
        {
            hBarPrime = (h1Prime + h2Prime + 360.0) / 2.0;
        }
        else
        {
            hBarPrime = (h1Prime + h2Prime - 360.0) / 2.0;
        }

        double t = 1.0
            - 0.17 * Math.Cos(ToRadians(hBarPrime - 30.0))
            + 0.24 * Math.Cos(ToRadians(2.0 * hBarPrime))
            + 0.32 * Math.Cos(ToRadians(3.0 * hBarPrime + 6.0))
            - 0.20 * Math.Cos(ToRadians(4.0 * hBarPrime - 63.0));

        double sl = 1.0 + (0.015 * Math.Pow(lBarPrime - 50.0, 2))
                       / Math.Sqrt(20.0 + Math.Pow(lBarPrime - 50.0, 2));
        double sc = 1.0 + 0.045 * cBarPrime;
        double sh = 1.0 + 0.015 * cBarPrime * t;

        double dTheta = 30.0 * Math.Exp(-Math.Pow((hBarPrime - 275.0) / 25.0, 2));
        double rc = 2.0 * Math.Sqrt(Math.Pow(cBarPrime, 7) / (Math.Pow(cBarPrime, 7) + Math.Pow(25.0, 7)));
        double rt = -Math.Sin(ToRadians(2.0 * dTheta)) * rc;

        double termL = dLPrime / (Kl * sl);
        double termC = dCPrime / (Kc * sc);
        double termH = dHPrime / (Kh * sh);

        return Math.Sqrt(termL * termL + termC * termC + termH * termH + rt * termC * termH);
    }

    public static double Cie2000(string srgbHexA, string srgbHexB) =>
        Cie2000(CieLab.FromSrgbHex(srgbHexA), CieLab.FromSrgbHex(srgbHexB));

    private static double ToRadians(double deg) => deg * Math.PI / 180.0;
    private static double ToDegrees(double rad) => rad * 180.0 / Math.PI;
}
