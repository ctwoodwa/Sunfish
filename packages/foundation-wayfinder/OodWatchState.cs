using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Lifecycle state of an OOD watch. Per ADR 0078 §1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OodWatchState
{
    /// <summary>The watch is in effect; no other Active watch may exist for the same (TenantId, OodRole).</summary>
    Active,

    /// <summary>An incoming watch took over via <c>HandoverWatchAsync</c>; the relieved watch is terminal.</summary>
    Relieved,

    /// <summary>The expiry sweep marked the watch terminal because <c>StartedAt + MaxWatchDuration</c> elapsed without a handover.</summary>
    Expired,
}
