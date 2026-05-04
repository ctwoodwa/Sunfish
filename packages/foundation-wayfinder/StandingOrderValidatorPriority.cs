using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Priority slot for an <see cref="IStandingOrderValidator"/>. The validator
/// chain runs in ascending priority order; the explicit numeric values reserve
/// gaps for future validator categories. Per ADR 0065 §3.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StandingOrderValidatorPriority
{
    /// <summary>Shape; required keys; type check; value range. Runs first.</summary>
    Schema = 100,

    /// <summary>Tenant policy (e.g., "production never has theme=experimental").</summary>
    Policy = 200,

    /// <summary>Issuer authority — capability-graph check via <c>Sunfish.Foundation.Capabilities</c>.</summary>
    Authority = 300,

    /// <summary>Concurrent-issuance detection; marks the loser of the LWW tie-break as <see cref="StandingOrderState.Conflicted"/>.</summary>
    Conflict = 400,
}
