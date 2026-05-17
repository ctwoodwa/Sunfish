namespace Sunfish.Blocks.Reports.Exceptions;

/// <summary>
/// Thrown by <see cref="ReportCartridgeRegistry.Resolve{TParams,TResult}"/>
/// when the requested <see cref="ReportKind"/> + (TParams, TResult)
/// tuple is not registered. Type-mismatch at registration time is
/// treated as not-registered (not a misroute) — keying by all three
/// defends against accidental param/result-type mismatch in
/// generic-dispatch registries.
/// </summary>
public sealed class UnknownReportKindException : System.Exception
{
    /// <summary>The report kind that was requested.</summary>
    public ReportKind Kind { get; }

    /// <summary>The TParams type that was requested.</summary>
    public System.Type ParamsType { get; }

    /// <summary>The TResult type that was requested.</summary>
    public System.Type ResultType { get; }

    /// <summary>Construct with the unresolved (kind, paramsType, resultType) tuple.</summary>
    public UnknownReportKindException(ReportKind kind, System.Type paramsType, System.Type resultType)
        : base($"No cartridge registered for ReportKind={kind} (TParams={paramsType.Name}, TResult={resultType.Name}).")
    {
        Kind = kind;
        ParamsType = paramsType;
        ResultType = resultType;
    }
}
