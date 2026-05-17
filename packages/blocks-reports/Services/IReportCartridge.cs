namespace Sunfish.Blocks.Reports;

/// <summary>
/// Strongly-typed read-side report cartridge per Stage 02 §6.1. A
/// cartridge is a pure projection from upstream cluster read APIs to
/// a result payload — no writes, no events.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-side discipline (compile-time invariant).</b> Cartridge
/// implementations MUST NOT inject <c>IDomainEventPublisher</c> or
/// any repository surface with a write API. Code review on every
/// cartridge PR MUST reject any ctor injection of a publisher /
/// write-capable repository. This is the structural rule that keeps
/// the cluster scoped to a single failure mode (read-side latency,
/// not write-side correctness).
/// </para>
/// <para>
/// <b>Tenant isolation.</b> Every cartridge MUST treat
/// <see cref="ReportExecutionContext.TenantId"/> as the sole tenant
/// scope; cartridge parameters that include entity IDs MUST validate
/// those IDs belong to the same tenant.
/// </para>
/// <para>
/// <b>Determinism.</b> Two runs of the same cartridge with the same
/// <see cref="ReportExecutionContext"/> and the same parameters MUST
/// produce equal results. The shared
/// <c>ReportCartridgeDeterminismTests</c> base in the test project
/// pins this contract for every cartridge.
/// </para>
/// </remarks>
public interface IReportCartridge<TParams, TResult>
    where TParams : class
    where TResult : class
{
    /// <summary>The cartridge kind this implementation handles.</summary>
    ReportKind Kind { get; }

    /// <summary>Compute the report result for the given context + parameters.</summary>
    /// <exception cref="Exceptions.ReportParameterValidationException">Thrown for invalid parameters (e.g., cross-tenant entity ID, malformed period).</exception>
    System.Threading.Tasks.Task<TResult> ExecuteAsync(
        ReportExecutionContext context,
        TParams parameters,
        System.Threading.CancellationToken ct = default);
}
