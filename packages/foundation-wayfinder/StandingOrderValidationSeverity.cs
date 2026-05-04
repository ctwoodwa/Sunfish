using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Severity of an issue surfaced by an <see cref="IStandingOrderValidator"/>.
/// Per ADR 0065 §3.
/// </summary>
/// <remarks>
/// Per ADR 0065 §3: any <see cref="Block"/>-severity issue rejects the order
/// (state flips to <see cref="StandingOrderState.Rejected"/>; rejection still
/// emits an audit event). <see cref="Error"/> reduces to <see cref="Block"/>
/// when the issuing capability is below tenant-admin.
/// <see cref="Warning"/> / <see cref="Info"/> annotate the order without
/// rejecting.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StandingOrderValidationSeverity
{
    /// <summary>Informational annotation; no behavioral effect.</summary>
    Info,

    /// <summary>Warning annotation; order accepted but operator should review.</summary>
    Warning,

    /// <summary>Error; reduces to <see cref="Block"/> for sub-admin issuers.</summary>
    Error,

    /// <summary>Hard block; rejects the order (still audited).</summary>
    Block,
}
