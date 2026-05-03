namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Semver triple applied to a taxonomy definition. Major bumps signal
/// breaking node-set changes (renames, removals); minor bumps add new nodes;
/// patch bumps correct display strings or descriptions.
/// </summary>
/// <param name="Major">Major segment.</param>
/// <param name="Minor">Minor segment.</param>
/// <param name="Patch">Patch segment.</param>
public readonly record struct TaxonomyVersion(int Major, int Minor, int Patch)
{
    /// <summary>Dotted version string.</summary>
    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    /// <summary>The 1.0.0 version (canonical first-published value).</summary>
    public static TaxonomyVersion V1_0_0 { get; } = new(1, 0, 0);

    /// <summary>Parses a <c>Major.Minor.Patch</c> string.</summary>
    /// <exception cref="FormatException">Thrown when the input is not three integer segments separated by periods.</exception>
    public static TaxonomyVersion Parse(string semver)
    {
        ArgumentNullException.ThrowIfNull(semver);
        var parts = semver.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException($"TaxonomyVersion must be Major.Minor.Patch; got '{semver}'.");
        }
        if (!int.TryParse(parts[0], out var major) || major < 0 ||
            !int.TryParse(parts[1], out var minor) || minor < 0 ||
            !int.TryParse(parts[2], out var patch) || patch < 0)
        {
            throw new FormatException($"TaxonomyVersion segments must be non-negative integers; got '{semver}'.");
        }
        return new TaxonomyVersion(major, minor, patch);
    }
}
