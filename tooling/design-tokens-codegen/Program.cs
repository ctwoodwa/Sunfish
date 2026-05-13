using System;
using System.IO;
using Sunfish.Tooling.DesignTokensCodegen;

// CLI: design-tokens-codegen --tokens <path> --out-css <path>
//        --out-tokens-md <path> --out-cvd-md <path>
//        [--verify-contrast] [--cvd-threshold <n>] [--ci]
//
// --verify-contrast  exit 1 if any WCAG contrast pair fails (CI build gate)
// --ci               quiet mode for CI (no ANSI, terse output)

bool verifyContrast = false;
bool verifyCvd = false;
bool ci = false;
double cvdThreshold = CvdAuditor.DefaultThreshold;
string? tokensPath = null;
string? outCss = null;
string? outTokensMd = null;
string? outCvdMd = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--tokens":        tokensPath    = args[++i]; break;
        case "--out-css":       outCss        = args[++i]; break;
        case "--out-tokens-md": outTokensMd   = args[++i]; break;
        case "--out-cvd-md":    outCvdMd      = args[++i]; break;
        case "--verify-contrast": verifyContrast = true;   break;
        case "--verify-cvd":    verifyCvd = true;          break;
        case "--ci":            ci = true;                 break;
        case "--cvd-threshold": cvdThreshold  = double.Parse(args[++i]); break;
    }
}

if (tokensPath == null)
{
    Console.Error.WriteLine("usage: design-tokens-codegen --tokens <path> [--out-css <path>]"
                          + " [--out-tokens-md <path>] [--out-cvd-md <path>]"
                          + " [--verify-contrast] [--cvd-threshold <n>] [--ci]");
    return 1;
}

string json = File.ReadAllText(tokensPath);
var catalog = TokensReader.Read(json);

int exitCode = 0;

// ── CSS codegen ──────────────────────────────────────────────────────────────
if (outCss != null)
{
    var dir = Path.GetDirectoryName(outCss);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    File.WriteAllText(outCss, CssGenerator.Generate(catalog));
    if (!ci) Console.WriteLine($"  css    → {outCss}");
}

// ── WCAG contrast gate ───────────────────────────────────────────────────────
var violations = ContrastVerifier.Verify(catalog.ContrastPairs);
if (violations.Count > 0)
{
    if (!ci) Console.Error.WriteLine($"  WCAG contrast: {violations.Count} violation(s)");
    foreach (var v in violations)
    {
        Console.Error.WriteLine(
            $"    FAIL {v.TextVar} on {v.SurfaceVar} ({v.Mode}): {v.Ratio:F2}:1 < {v.Required}:1 required");
    }
    if (verifyContrast) exitCode = 1;
}
else
{
    if (!ci) Console.WriteLine($"  contrast ✓ ({catalog.ContrastPairs.Count} pairs checked)");
}

// ── CVD ΔE2000 audit ─────────────────────────────────────────────────────────
var cvdRows = CvdAuditor.Audit(catalog.RoleBandColors, cvdThreshold);
bool cvdFail = false;
foreach (var r in cvdRows)
{
    if (!r.Pass)
    {
        cvdFail = true;
        Console.Error.WriteLine(
            $"    CVD FAIL {r.HueA} × {r.HueB}: min ΔE2000 = {r.MinDeltaE:F1} < {cvdThreshold}");
    }
}
if (!cvdFail && !ci) Console.WriteLine($"  CVD ΔE2000 ✓ ({cvdRows.Count} pairs checked)");
if (cvdFail && verifyCvd) exitCode = 1;

// ── Markdown codegen ─────────────────────────────────────────────────────────
if (outTokensMd != null)
{
    var dir = Path.GetDirectoryName(outTokensMd);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    File.WriteAllText(outTokensMd, MarkdownGenerator.GenerateTokensReference(catalog));
    if (!ci) Console.WriteLine($"  tokens.md → {outTokensMd}");
}

if (outCvdMd != null)
{
    var dir = Path.GetDirectoryName(outCvdMd);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    File.WriteAllText(outCvdMd, MarkdownGenerator.GenerateCvdReport(cvdRows, cvdThreshold));
    if (!ci) Console.WriteLine($"  cvd.md → {outCvdMd}");
}

return exitCode;
