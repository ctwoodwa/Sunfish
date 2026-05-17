using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reports;

/// <summary>
/// Resolves a <see cref="ReportKind"/> to a registered cartridge and
/// invokes it within a freshly built
/// <see cref="ReportExecutionContext"/>. The runner is the cluster's
/// single entry point for report execution — direct cartridge
/// invocation by consumers is allowed but discouraged (the runner
/// is where snapshot-marker capture, run-duration measurement, and
/// provisionality propagation live).
/// </summary>
public interface IReportRunner
{
    /// <summary>Run the cartridge for the given kind + parameters under the caller's tenant + principal.</summary>
    /// <exception cref="Exceptions.UnknownReportKindException">No cartridge registered for the given kind + types.</exception>
    /// <exception cref="Exceptions.ReportParameterValidationException">Cartridge rejected the parameters.</exception>
    /// <exception cref="Exceptions.ReportCartridgeExecutionException">Cartridge threw any other exception.</exception>
    System.Threading.Tasks.Task<ReportRunResult<TResult>> RunAsync<TParams, TResult>(
        ReportKind kind,
        TParams parameters,
        TenantId tenantId,
        PrincipalId requestedBy,
        System.Threading.CancellationToken ct = default)
        where TParams : class
        where TResult : class;
}
