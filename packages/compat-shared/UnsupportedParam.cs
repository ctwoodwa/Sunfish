using System;

namespace Sunfish.Compat.Shared;

/// <summary>
/// Helper that builds a consistently-shaped <see cref="NotSupportedException"/> for vendor
/// parameter values that have no Sunfish equivalent. Always include a migration hint so the
/// consumer can port their code forward.
/// </summary>
public static class UnsupportedParam
{
    /// <summary>
    /// Builds a <see cref="NotSupportedException"/> describing an unsupported parameter value.
    /// Always throw the returned exception at the call site:
    /// <c>throw UnsupportedParam.Throw(nameof(Param), value, "hint");</c>.
    /// The <paramref name="migrationHint"/> is where vendors should include a pointer to their
    /// own mapping doc (e.g., <c>"See docs/compat-syncfusion-mapping.md — Grid row detail."</c>).
    /// </summary>
    public static NotSupportedException Throw(string paramName, string value, string migrationHint)
        => new NotSupportedException(
            $"Compat parameter `{paramName}={value}` has no Sunfish equivalent. " +
            $"Migration hint: {migrationHint}");
}
