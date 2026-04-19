namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// Sealed abstract base for all tax-report body types. Discriminated via <see cref="TaxReportKind"/>.
/// </summary>
/// <remarks>
/// <para>
/// To add a new body kind, subclass this record and add a corresponding
/// <see cref="TaxReportKind"/> member. The canonical-JSON helper and text renderer each
/// switch on <see cref="Kind"/>; update those when adding a new variant.
/// </para>
/// <para>
/// Pattern-matching convention:
/// <code>
/// TaxReportBody body = ...;
/// switch (body)
/// {
///     case ScheduleEBody e:            // handle Schedule E
///     case Form1099NecBody nec:        // handle 1099-NEC
///     case StatePersonalPropertyBody s: // handle state personal property
/// }
/// </code>
/// </para>
/// </remarks>
public abstract record TaxReportBody
{
    /// <summary>
    /// The discriminant that identifies which concrete body subtype this instance is.
    /// Consumers may use <c>switch (body.Kind)</c> or C# pattern-matching on the subtype.
    /// </summary>
    public abstract TaxReportKind Kind { get; }
}
