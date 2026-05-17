namespace Sunfish.Blocks.Reports.Exceptions;

/// <summary>
/// Thrown by <see cref="IReportCartridge{TParams,TResult}.ExecuteAsync"/>
/// when cartridge parameters fail validation (cross-tenant entity
/// ID, malformed period, missing required field, etc.). The runner
/// passes this exception through unwrapped — callers see the
/// original type so they can render parameter-validation errors with
/// per-field detail.
/// </summary>
public sealed class ReportParameterValidationException : System.Exception
{
    /// <summary>The parameter name (or field path) that failed validation.</summary>
    public string ParameterName { get; }

    /// <summary>Construct with the parameter name + human-readable reason.</summary>
    public ReportParameterValidationException(string parameterName, string reason)
        : base($"Report parameter '{parameterName}' failed validation: {reason}")
    {
        ParameterName = parameterName;
    }
}
