using System;

namespace Sunfish.Compat.Telerik.Internal;

/// <summary>
/// Helper that builds a consistently-shaped <see cref="NotSupportedException"/> for Telerik
/// parameter values that have no Sunfish equivalent. Always include a migration hint so the
/// consumer can port their code forward.
/// </summary>
internal static class UnsupportedParam
{
    /// <summary>
    /// Builds a <see cref="NotSupportedException"/> describing an unsupported Telerik
    /// parameter value. Always throw the returned exception at the call site:
    /// <c>throw UnsupportedParam.Throw(nameof(Param), value, "hint");</c>.
    /// </summary>
    public static NotSupportedException Throw(string paramName, string value, string migrationHint)
        => new NotSupportedException(
            $"Telerik parameter `{paramName}={value}` has no Sunfish equivalent. " +
            $"Migration hint: {migrationHint} " +
            $"See docs/compat-telerik-mapping.md.");
}
