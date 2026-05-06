using System.Text.Json.Serialization;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Six-state state machine for the Sick Bay Medevac flow per ADR 0082 §2.
/// Valid transitions:
/// <c>Idle → Requested → PendingAuthorization → Authorized → InProgress
/// → Complete</c>; <c>Cancel</c> moves any non-terminal state back to
/// <c>Idle</c>; self-approval at <c>PendingAuthorization → Authorized</c>
/// throws <see cref="System.InvalidOperationException"/> per the §Trust
/// four-eyes invariant.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MedevacState
{
    /// <summary>No medevac in progress; the initial / cancelled state.</summary>
    Idle,

    /// <summary>An IDC has filed a medevac request; awaiting routing.</summary>
    Requested,

    /// <summary>Routed to authorizing principal (Captain); awaiting four-eyes approval.</summary>
    PendingAuthorization,

    /// <summary>Approved by a non-self authorizing principal; ready to execute.</summary>
    Authorized,

    /// <summary>Medevac in progress; stretcher bearer dispatched per <see cref="IStretcherBearerPolicy"/>.</summary>
    InProgress,

    /// <summary>Medevac complete; final terminal state per cycle.</summary>
    Complete,
}
