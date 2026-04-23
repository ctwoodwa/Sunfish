namespace Sunfish.Bridge;

/// <summary>
/// Install-time deployment posture for the Bridge host, per
/// <see href="../../docs/adrs/0026-bridge-posture.md">ADR 0026</see>.
/// </summary>
/// <remarks>
/// <para>
/// Posture is selected once at install time via <c>BridgeOptions.Mode</c>
/// (bound from configuration). The two postures share the repository and
/// the <c>Sunfish.Bridge</c> assembly but compose entirely disjoint service
/// graphs — SaaS wiring is the Blazor-Server + Postgres + DAB + SignalR
/// stack Bridge ships today; Relay wiring is the paper §6.1 tier-3
/// managed relay: stateless, kernel-sync-only, no authority semantics.
/// </para>
/// </remarks>
public enum BridgeMode
{
    /// <summary>Posture A: multi-tenant SaaS shell (ADR 0006; paper §14 trust-gap audience).</summary>
    SaaS,

    /// <summary>Posture B: paper §6.1 tier-3 managed relay; §17.2 sustainable-revenue SKU.</summary>
    Relay,
}
