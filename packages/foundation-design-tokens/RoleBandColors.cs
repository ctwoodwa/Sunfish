namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.color.role-band.*</c> tokens per ADR 0077 §5.2. Seven distinct
/// hues, one per ShipRole authority gradient (per ADR 0077 §1).
/// </summary>
/// <remarks>
/// <b>PLACEHOLDER per W#46 Phase 2a.</b> Current values are
/// Tailwind-distinct hues sufficient for dev rendering. Phase 2b will
/// replace with WCAG 1.4.3 / 1.4.11 contrast-verified + CVD ΔE2000-
/// audited values per ADR 0036 CVD precedent. Production UI MUST NOT
/// ship with these placeholder values without a Phase 2b CVD audit
/// pass.
/// </remarks>
public static class RoleBandColors
{
    /// <summary>Captain (tenant owner / BDFL).</summary>
    public static readonly ColorToken Captain         = new(Light: "#7c3aed", Dark: "#a78bfa");

    /// <summary>XO (deputy).</summary>
    public static readonly ColorToken XO              = new(Light: "#2563eb", Dark: "#60a5fa");

    /// <summary>Department head (EngineerOfficer / Navigator / TacticalOfficer).</summary>
    public static readonly ColorToken DepartmentHead  = new(Light: "#0891b2", Dark: "#22d3ee");

    /// <summary>Division Officer (junior officer in rotation).</summary>
    public static readonly ColorToken DivisionOfficer = new(Light: "#16a34a", Dark: "#4ade80");

    /// <summary>IDC (Independent Duty Corpsman → Sick Bay).</summary>
    public static readonly ColorToken IDC             = new(Light: "#ea580c", Dark: "#fb923c");

    /// <summary>Scribe (→ Ship's Office).</summary>
    public static readonly ColorToken Scribe          = new(Light: "#9333ea", Dark: "#c084fc");

    /// <summary>Watch (OOD / EOOW; temporally-bounded designations).</summary>
    public static readonly ColorToken Watch           = new(Light: "#dc2626", Dark: "#f87171");
}
