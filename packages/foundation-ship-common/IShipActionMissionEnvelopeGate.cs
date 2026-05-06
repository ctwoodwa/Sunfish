using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Composes <see cref="IFeatureGate{TFeature}"/> with the
/// <see cref="ShipAction"/> taxonomy per ADR 0077 §2.1 step 2. Hosts wire
/// per-action <see cref="MissionEnvelope"/> evaluations through this
/// adapter; <see cref="DefaultPermissionResolver"/> consumes the
/// <see cref="MissionEnvelopeVerdict"/> directly so it does not need to
/// know which <c>IFeature</c> generic argument to instantiate.
/// </summary>
/// <remarks>
/// Phase 1 ships the contract; the default-empty implementation
/// (<see cref="NullShipActionMissionEnvelopeGate"/>) returns
/// <see cref="MissionEnvelopeVerdict.Available"/> for every action so
/// resolvers without a configured gate skip step 2. Hosts opt in by
/// registering a concrete <see cref="IShipActionMissionEnvelopeGate"/>
/// against the DI container.
/// </remarks>
public interface IShipActionMissionEnvelopeGate
{
    /// <summary>Evaluate the Mission-Envelope verdict for the supplied action.</summary>
    ValueTask<MissionEnvelopeVerdict> EvaluateAsync(ShipAction action, CancellationToken ct = default);
}

/// <summary>
/// Decision payload returned by <see cref="IShipActionMissionEnvelopeGate.EvaluateAsync"/>.
/// </summary>
/// <param name="IsAvailable">True when the action is available in the current envelope.</param>
/// <param name="ReasonDisplay">Localized human-readable cause when unavailable; ignored when available.</param>
/// <param name="RemediationDisplay">Localized suggested-next-action when unavailable; ignored when available.</param>
/// <param name="CallToActionLabel">Localized affordance label (e.g., <c>"Upgrade edition"</c>) when unavailable; null otherwise.</param>
public sealed record MissionEnvelopeVerdict(
    bool IsAvailable,
    string ReasonDisplay,
    string RemediationDisplay,
    string? CallToActionLabel)
{
    /// <summary>Singleton "available" verdict.</summary>
    public static readonly MissionEnvelopeVerdict Available = new(
        IsAvailable: true,
        ReasonDisplay: string.Empty,
        RemediationDisplay: string.Empty,
        CallToActionLabel: null);
}

/// <summary>
/// Default no-op gate that returns <see cref="MissionEnvelopeVerdict.Available"/>
/// for every action — Phase 1 hosts opt in to Mission-Envelope gating by
/// registering a concrete implementation against DI.
/// </summary>
public sealed class NullShipActionMissionEnvelopeGate : IShipActionMissionEnvelopeGate
{
    /// <summary>Singleton instance.</summary>
    public static readonly NullShipActionMissionEnvelopeGate Instance = new();

    /// <inheritdoc />
    public ValueTask<MissionEnvelopeVerdict> EvaluateAsync(ShipAction action, CancellationToken ct = default) =>
        ValueTask.FromResult(MissionEnvelopeVerdict.Available);
}
