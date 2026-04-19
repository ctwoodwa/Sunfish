namespace Sunfish.Kernel.Events;

/// <summary>
/// Kernel primitive §3.6 — event bus. <b>Not yet implemented.</b>
/// </summary>
/// <remarks>
/// <para>
/// This interface is a reserved landing zone for the Event Bus primitive
/// described in the Sunfish platform spec §3.6. The shipping contract is
/// scoped by gap <b>G3</b> in the platform gap analysis
/// (<c>icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md</c>).
/// </para>
/// <para>
/// The interface is intentionally empty: we pin the namespace and type name
/// now so G3 can land against a stable surface without renaming downstream
/// consumers. Do not implement against this stub — it will gain members when
/// G3 is built, and any premature implementations would need rework.
/// </para>
/// </remarks>
public interface IEventBus
{
    // Intentionally empty — see gap G3 for the shipping contract.
}
