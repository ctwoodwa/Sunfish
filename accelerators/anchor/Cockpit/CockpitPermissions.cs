namespace Sunfish.Anchor.Cockpit;

/// <summary>
/// Owner cockpit role + area permissions matrix (W#29 hand-off cluster OQ1).
///
/// Mirror lives at <c>accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitPermissions.cs</c>;
/// keep both in sync. Extract to a shared package if the matrix grows past
/// trivial maintenance burden.
///
/// Phase 1 only enforces <see cref="CanEnterCockpit"/> at the route guard.
/// The full matrix is pre-encoded so PR 2–5 page-level checks compile against
/// a single source.
/// </summary>
public static class CockpitPermissions
{
    /// <summary>Canonical role identifiers carried on the caller's claims.</summary>
    public static class Roles
    {
        public const string Owner       = "owner";
        public const string Spouse      = "spouse";
        public const string Bookkeeper  = "bookkeeper";
        public const string TaxAdvisor  = "tax-advisor";
        public const string Contractor  = "contractor";
        public const string Leaseholder = "leaseholder";
        public const string Prospect    = "prospect";
    }

    /// <summary>Cockpit functional areas keyed by the matrix.</summary>
    public enum Area
    {
        Properties,
        Equipment,
        Leases,
        LeasingPipeline,
        WorkOrders,
        Vendors,
        Inspections,
        Receipts,
        Reports,
    }

    /// <summary>Access tier resolved per (role, area).</summary>
    public enum Access
    {
        None,
        ReadOwn,
        Read,
        Full,
        Export,
    }

    /// <summary>
    /// Phase 1 cockpit gate. Only owner + spouse have the full cockpit
    /// surface; other roles are coded but route to "no access" until
    /// Phase 3 enables their entry points.
    /// </summary>
    public static bool CanEnterCockpit(string? role) => role is Roles.Owner or Roles.Spouse;

    /// <summary>
    /// Resolves the cockpit access tier for (role, area). Unknown roles
    /// resolve to <see cref="Access.None"/>. Phase 1 only consults this for
    /// page-level rendering decisions; the route guard uses
    /// <see cref="CanEnterCockpit"/>.
    /// </summary>
    public static Access Resolve(string? role, Area area) => (role, area) switch
    {
        // owner + spouse — full cockpit (Phase 1 visible)
        (Roles.Owner or Roles.Spouse, _) => Access.Full,

        // bookkeeper — read on operational data; full receipts (Phase 3); CSV export on reports
        (Roles.Bookkeeper, Area.Properties or Area.Equipment or Area.Leases or Area.WorkOrders or Area.Vendors or Area.Inspections) => Access.Read,
        (Roles.Bookkeeper, Area.LeasingPipeline) => Access.None,
        (Roles.Bookkeeper, Area.Receipts)        => Access.Full,
        (Roles.Bookkeeper, Area.Reports)         => Access.Export,

        // tax-advisor — summary read on properties; 1099 read on vendors; full export on reports
        (Roles.TaxAdvisor, Area.Properties)      => Access.Read,
        (Roles.TaxAdvisor, Area.Vendors)         => Access.Read,
        (Roles.TaxAdvisor, Area.Receipts)        => Access.Export,
        (Roles.TaxAdvisor, Area.Reports)         => Access.Full,

        // contractor — own work orders + own profile only
        (Roles.Contractor, Area.WorkOrders or Area.Vendors or Area.Inspections or Area.Equipment) => Access.ReadOwn,

        // leaseholder — own unit / own lease / own work orders / own receipts
        (Roles.Leaseholder, Area.Properties or Area.Leases or Area.WorkOrders or Area.Receipts) => Access.ReadOwn,

        // prospect — own pipeline only
        (Roles.Prospect, Area.LeasingPipeline) => Access.ReadOwn,

        _ => Access.None,
    };
}
