namespace Sunfish.Blocks.Reports.Exceptions;

/// <summary>
/// Wraps any non-<see cref="ReportParameterValidationException"/>
/// exception that escapes a cartridge's
/// <see cref="IReportCartridge{TParams,TResult}.ExecuteAsync"/>. The
/// runner re-raises this so callers see a stable exception type
/// regardless of which cartridge or upstream cluster failed.
/// </summary>
public sealed class ReportCartridgeExecutionException : System.Exception
{
    /// <summary>The kind of report whose cartridge failed.</summary>
    public ReportKind Kind { get; }

    /// <summary>Construct with the kind + the inner exception that was caught.</summary>
    public ReportCartridgeExecutionException(ReportKind kind, System.Exception inner)
        : base($"Cartridge for ReportKind={kind} threw {inner.GetType().Name}: {inner.Message}", inner)
    {
        Kind = kind;
    }
}
