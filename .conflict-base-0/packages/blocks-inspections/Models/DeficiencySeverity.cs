namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Severity level of a <see cref="Deficiency"/>.
/// </summary>
public enum DeficiencySeverity
{
    /// <summary>Minor issue; low impact on habitability or safety.</summary>
    Low,

    /// <summary>Notable issue requiring attention within a normal maintenance cycle.</summary>
    Medium,

    /// <summary>Significant issue requiring prompt remediation.</summary>
    High,

    /// <summary>Immediate hazard; requires urgent action before the unit is occupied.</summary>
    Critical,
}
