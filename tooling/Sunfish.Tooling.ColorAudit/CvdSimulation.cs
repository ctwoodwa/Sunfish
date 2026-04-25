namespace Sunfish.Tooling.ColorAudit;

/// <summary>
/// Color-vision-deficiency simulation modes Sunfish audits. Matches the chromium CDP
/// <c>Emulation.setEmulatedVisionDeficiency</c> identifiers used by the bUnit-to-axe
/// bridge (Sunfish.UIAdapters.Blazor.A11y.CvdMode) for cross-tool consistency.
/// </summary>
public enum CvdMode
{
    /// <summary>Full normal trichromatic vision; identity transform.</summary>
    None,
    /// <summary>Red-blindness — L cone absent.</summary>
    Protanopia,
    /// <summary>Green-blindness — M cone absent.</summary>
    Deuteranopia,
    /// <summary>Blue-blindness — S cone absent.</summary>
    Tritanopia,
}

/// <summary>
/// Simulate how a color appears to a viewer with a given vision deficiency.
/// Uses Machado, Oliveira &amp; Fernandes (2009) matrices at full deficiency severity 1.0,
/// applied in linear-RGB space. Reference:
/// "A Physiologically-based Model for Simulation of Color Vision Deficiency",
/// IEEE Transactions on Visualization and Computer Graphics, 2009.
/// </summary>
public static class CvdSimulation
{
    /// <summary>Simulate <paramref name="srgbHex"/> under <paramref name="mode"/>; return sRGB hex.</summary>
    public static string Simulate(string srgbHex, CvdMode mode)
    {
        if (mode == CvdMode.None) return srgbHex;
        var rgb = LinearRgb.FromSrgbHex(srgbHex);
        var simulated = ApplyMatrix(rgb, MatrixFor(mode));
        return simulated.ToSrgbHex();
    }

    /// <summary>Compute ΔE2000 between two colors as perceived under <paramref name="mode"/>.</summary>
    public static double DeltaE2000Under(string hexA, string hexB, CvdMode mode)
    {
        var simA = Simulate(hexA, mode);
        var simB = Simulate(hexB, mode);
        return DeltaE.Cie2000(simA, simB);
    }

    /// <summary>Apply a 3x3 transform to a linear-RGB triple.</summary>
    private static LinearRgb ApplyMatrix(LinearRgb rgb, double[,] m) => new(
        m[0, 0] * rgb.R + m[0, 1] * rgb.G + m[0, 2] * rgb.B,
        m[1, 0] * rgb.R + m[1, 1] * rgb.G + m[1, 2] * rgb.B,
        m[2, 0] * rgb.R + m[2, 1] * rgb.G + m[2, 2] * rgb.B);

    private static double[,] MatrixFor(CvdMode mode) => mode switch
    {
        CvdMode.Protanopia => new[,]
        {
            { 0.152286, 1.052583, -0.204868 },
            { 0.114503, 0.786281, 0.099216 },
            { -0.003882, -0.048116, 1.051998 },
        },
        CvdMode.Deuteranopia => new[,]
        {
            { 0.367322, 0.860646, -0.227968 },
            { 0.280085, 0.672501, 0.047413 },
            { -0.011820, 0.042940, 0.968881 },
        },
        CvdMode.Tritanopia => new[,]
        {
            { 1.255528, -0.076749, -0.178779 },
            { -0.078411, 0.930809, 0.147602 },
            { 0.004733, 0.691367, 0.303900 },
        },
        _ => throw new System.ArgumentException("None has no matrix; check before calling.", nameof(mode)),
    };
}
