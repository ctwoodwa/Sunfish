namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// A contractor or service provider that can be assigned work orders or invited to submit quotes.
/// </summary>
/// <param name="Id">Unique identifier for this vendor.</param>
/// <param name="DisplayName">Human-readable name shown in the UI.</param>
/// <param name="ContactName">Name of the primary contact person, if known.</param>
/// <param name="ContactEmail">Email address for the primary contact, if known.</param>
/// <param name="ContactPhone">Phone number for the primary contact, if known.</param>
/// <param name="Specialty">Trade or service category this vendor specializes in.</param>
/// <param name="Status">Current lifecycle status of this vendor record.</param>
public sealed record Vendor(
    VendorId Id,
    string DisplayName,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    VendorSpecialty Specialty,
    VendorStatus Status);
