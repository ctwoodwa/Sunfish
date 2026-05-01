using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Bridge;

/// <summary>
/// Per ADR 0064-A1.4 — Bridge-boundary middleware that gates inbound writes
/// against the configured <see cref="DataResidencyConstraint"/>s. Replies
/// with HTTP 451 (Unavailable for Legal Reasons; RFC 7725) when a write is
/// blocked.
/// </summary>
/// <remarks>
/// <para>
/// The substrate ships the contract + a default ASP.NET Core middleware
/// implementation. The Bridge accelerator wires it into its request
/// pipeline at the gate where <c>(recordClass, jurisdictionCode)</c> can
/// be resolved from the request context (typically a header / claim /
/// authenticated tenant probe).
/// </para>
/// <para>
/// Production hosts that want a different transport-level response shape
/// (e.g., a structured RFC 7807 ProblemDetails body) can register their
/// own <see cref="IDataResidencyEnforcerMiddleware"/>; the default
/// implementation is a vanilla ASP.NET Core <see cref="IMiddleware"/>.
/// </para>
/// </remarks>
public interface IDataResidencyEnforcerMiddleware : IMiddleware
{
    /// <summary>
    /// Resolves <c>(recordClass, jurisdictionCode)</c> from the request
    /// context. Hosts override to plug their own resolution strategy
    /// (header / claim / tenant probe). Returns null when the request is
    /// not subject to residency enforcement.
    /// </summary>
    ValueTask<DataResidencyContext?> ResolveContextAsync(HttpContext context);
}

/// <summary>
/// Resolved request context for <see cref="IDataResidencyEnforcerMiddleware"/>.
/// Carries the <c>recordClass</c> the inbound write touches + the resolved
/// jurisdiction code.
/// </summary>
public sealed record DataResidencyContext
{
    public required string RecordClass { get; init; }

    public required string JurisdictionCode { get; init; }
}
