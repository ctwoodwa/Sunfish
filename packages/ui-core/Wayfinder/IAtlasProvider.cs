using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Generic base for Atlas provider specializations per ADR 0066 §1 + ADR 0067.
/// Each Atlas sub-surface (integration config, security policy, account
/// identity, domain config, user preferences) implements a typed
/// specialization of this interface — the Helm shell renders the union
/// without knowing the concrete projection types.
/// </summary>
/// <remarks>
/// <para>
/// <b>Side-effect-free contract:</b> implementations MUST be projection-only
/// — no mutations, no audit emission, no Standing Order issuance, no
/// capability-graph writes. Calls flowing through the read-side Atlas
/// surface MUST be idempotent + safe to call many times in rapid
/// succession (the Helm widget refresh tick fires every <c>HelmOptions.PeriodicRefreshInterval</c>).
/// </para>
/// <para>
/// <b>Invariant generic:</b> <typeparamref name="TView"/> is invariant
/// because <see cref="Task{TView}"/> itself is invariant in
/// <c>T</c>. The hand-off cited <c>out TView</c>; the C# compiler
/// rejects that (CS1961: <c>Task&lt;TView&gt;</c> is not a covariant
/// position). Phase 2 may consider an
/// <see cref="System.Collections.Generic.IAsyncEnumerable{T}"/>-based
/// covariant alternative; Phase 1a ships invariant. Consumers holding
/// <c>IAtlasProvider&lt;BaseView&gt;</c> still see derived views via
/// the regular C# inheritance / interface-substitution rules at
/// concrete-type construction time (the Phase 2 W#48
/// <c>IIntegrationAtlasProvider</c> derives from
/// <c>IAtlasProvider&lt;IntegrationAtlasView&gt;</c>; the shell consumes
/// the concrete derived interface, not a covariant downcast).
/// </para>
/// </remarks>
/// <typeparam name="TView">
/// The projected view type returned by <see cref="GetAtlasViewAsync"/>.
/// MUST be a reference type; the projection is always heap-allocated
/// (contains collections).
/// </typeparam>
public interface IAtlasProvider<TView>
    where TView : class
{
    /// <summary>
    /// Returns the current Atlas view for the ambient tenant/actor
    /// context. Implementations MUST be side-effect-free; projection
    /// only — no mutations, no audit emission, no Standing Order
    /// issuance.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<TView> GetAtlasViewAsync(CancellationToken ct = default);
}
