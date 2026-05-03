namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// The trade or specialization a <see cref="Vendor"/> provides.
/// </summary>
public enum VendorSpecialty
{
    /// <summary>General contractor with broad capabilities.</summary>
    GeneralContractor,

    /// <summary>Plumbing work.</summary>
    Plumbing,

    /// <summary>Electrical work.</summary>
    Electrical,

    /// <summary>Heating, ventilation, and air conditioning.</summary>
    HVAC,

    /// <summary>Landscaping and grounds maintenance.</summary>
    Landscaping,

    /// <summary>Interior and exterior painting.</summary>
    Painting,

    /// <summary>Roofing installation and repair.</summary>
    Roofing,

    /// <summary>Pest control and extermination.</summary>
    PestControl,

    /// <summary>Appliance installation and repair.</summary>
    Appliances,

    /// <summary>Cleaning and janitorial services.</summary>
    Cleaning,

    /// <summary>Other or unlisted specialty.</summary>
    Other,
}
