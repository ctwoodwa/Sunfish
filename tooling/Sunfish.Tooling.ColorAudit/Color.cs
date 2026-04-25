using System;
using System.Globalization;

namespace Sunfish.Tooling.ColorAudit;

/// <summary>
/// Linear RGB color in [0, 1] floats. Conversion to/from sRGB hex strings via
/// <see cref="FromSrgbHex"/> / <see cref="ToSrgbHex"/> applies the standard sRGB gamma curve.
/// </summary>
public readonly record struct LinearRgb(double R, double G, double B)
{
    /// <summary>Parse <c>"#RRGGBB"</c> or <c>"#RGB"</c> sRGB hex into linear RGB.</summary>
    public static LinearRgb FromSrgbHex(string hex)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length != 6) throw new FormatException($"Expected 6-digit hex; got '{hex}'.");

        var r = byte.Parse(s.AsSpan(0, 2), NumberStyles.HexNumber);
        var g = byte.Parse(s.AsSpan(2, 2), NumberStyles.HexNumber);
        var b = byte.Parse(s.AsSpan(4, 2), NumberStyles.HexNumber);
        return new LinearRgb(SrgbToLinear(r / 255.0), SrgbToLinear(g / 255.0), SrgbToLinear(b / 255.0));
    }

    /// <summary>Convert back to sRGB hex string (uppercase, with #).</summary>
    public string ToSrgbHex()
    {
        int r = ((int)Math.Round(LinearToSrgb(R) * 255)).Clamp(0, 255);
        int g = ((int)Math.Round(LinearToSrgb(G) * 255)).Clamp(0, 255);
        int b = ((int)Math.Round(LinearToSrgb(B) * 255)).Clamp(0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>sRGB gamma decode (curve per IEC 61966-2-1).</summary>
    public static double SrgbToLinear(double v) =>
        v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    /// <summary>sRGB gamma encode (curve per IEC 61966-2-1).</summary>
    public static double LinearToSrgb(double v) =>
        v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
}

/// <summary>CIE XYZ tristimulus, D65 white point. Range varies but typically [0, ~1.0].</summary>
public readonly record struct CieXyz(double X, double Y, double Z)
{
    /// <summary>D65 white point (CIE 1931 2°).</summary>
    public static readonly CieXyz D65 = new(0.95047, 1.00000, 1.08883);

    public static CieXyz FromLinearRgb(LinearRgb rgb) => new(
        // sRGB → XYZ matrix (IEC 61966-2-1 / Bradford-adapted D65).
        0.4124564 * rgb.R + 0.3575761 * rgb.G + 0.1804375 * rgb.B,
        0.2126729 * rgb.R + 0.7151522 * rgb.G + 0.0721750 * rgb.B,
        0.0193339 * rgb.R + 0.1191920 * rgb.G + 0.9503041 * rgb.B);
}

/// <summary>CIE Lab (L*a*b*), D65 white point.</summary>
public readonly record struct CieLab(double L, double A, double B)
{
    public static CieLab FromXyz(CieXyz xyz)
    {
        double fx = LabF(xyz.X / CieXyz.D65.X);
        double fy = LabF(xyz.Y / CieXyz.D65.Y);
        double fz = LabF(xyz.Z / CieXyz.D65.Z);
        return new CieLab(116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }

    public static CieLab FromSrgbHex(string hex) =>
        FromXyz(CieXyz.FromLinearRgb(LinearRgb.FromSrgbHex(hex)));

    private static double LabF(double t)
    {
        const double delta = 6.0 / 29.0;
        return t > delta * delta * delta
            ? Math.Cbrt(t)
            : t / (3 * delta * delta) + 4.0 / 29.0;
    }
}

internal static class MathExtensions
{
    public static int Clamp(this int v, int min, int max) => v < min ? min : (v > max ? max : v);
}
