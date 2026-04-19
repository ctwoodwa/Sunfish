using Sunfish.Blocks.TaxReporting.Models;

namespace Sunfish.Blocks.TaxReporting.Rendering;

/// <summary>
/// Produces a human-readable plain-text representation of a <see cref="TaxReport"/>.
/// No PDF rendering in this pass — text output only.
/// PDF rendering is deferred to a future pass.
/// </summary>
public interface ITaxReportTextRenderer
{
    /// <summary>
    /// Renders the report as a plain-text string suitable for display or console output.
    /// </summary>
    string Render(TaxReport report);
}
