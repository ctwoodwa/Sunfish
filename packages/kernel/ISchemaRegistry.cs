namespace Sunfish.Kernel.Schema;

/// <summary>
/// Kernel primitive §3.4 — schema registry. <b>Not yet implemented.</b>
/// </summary>
/// <remarks>
/// <para>
/// This interface is a reserved landing zone for the Schema Registry primitive
/// described in the Sunfish platform spec §3.4. The shipping contract is
/// scoped by gap <b>G2</b> in the platform gap analysis
/// (<c>icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md</c>).
/// </para>
/// <para>
/// The interface is intentionally empty: we pin the namespace and type name
/// now so G2 can land against a stable surface without renaming downstream
/// consumers. Do not implement against this stub — it will gain members when
/// G2 is built, and any premature implementations would need rework.
/// </para>
/// </remarks>
public interface ISchemaRegistry
{
    // Intentionally empty — see gap G2 for the shipping contract.
}
