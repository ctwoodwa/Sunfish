using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>Marker interface per A1.2 for "a feature".</summary>
public interface IFeature { }

/// <summary>Marker interface for an <see cref="IFeatureBespokeProbe{TBespokeSignal}"/> result per A1.2.</summary>
public interface IBespokeSignal { }

/// <summary>Per-feature gate per A1.2 — produces a <see cref="FeatureVerdict"/>.</summary>
public interface IFeatureGate<TFeature> where TFeature : IFeature
{
    ValueTask<FeatureVerdict> EvaluateAsync(MissionEnvelope envelope, CancellationToken ct = default);
}

/// <summary>Per-dimension probe per A1.2 + A1.6 + A1.10.</summary>
public interface IDimensionProbe<TDimension>
{
    DimensionChangeKind Dimension { get; }
    ProbeCostClass CostClass { get; }
    ValueTask<TDimension> ProbeAsync(CancellationToken ct = default);
}

/// <summary>Feature-specific signal probe per A1.2 — extension for non-envelope dimensions.</summary>
public interface IFeatureBespokeProbe<TBespokeSignal> where TBespokeSignal : IBespokeSignal
{
    string FeatureKey { get; }
    ProbeCostClass CostClass { get; }
    ValueTask<TBespokeSignal> ProbeAsync(CancellationToken ct = default);
}

/// <summary>Subscriber for <see cref="EnvelopeChange"/> per A1.2 + A1.4.</summary>
public interface IMissionEnvelopeObserver
{
    ValueTask OnChangedAsync(EnvelopeChange change, CancellationToken ct = default);
}

/// <summary>Operator-only force-enable per A1.2 + A1.9.</summary>
public interface IFeatureForceEnableSurface
{
    ValueTask<ForceEnableRecord> RequestAsync(FeatureForceEnableRequest request, CancellationToken ct = default);
    ValueTask RevokeAsync(string featureKey, DimensionChangeKind dimension, CancellationToken ct = default);
    ValueTask<ForceEnableRecord?> ResolveAsync(string featureKey, DimensionChangeKind dimension, CancellationToken ct = default);
}

/// <summary>Central coordinator per A1.2 + A1.4. Phase 1 ships the contract.</summary>
public interface IMissionEnvelopeProvider
{
    ValueTask<MissionEnvelope> GetCurrentAsync(CancellationToken ct = default);
    ValueTask InvalidateAsync(CancellationToken ct = default);
    void Subscribe(IMissionEnvelopeObserver observer);
    void Unsubscribe(IMissionEnvelopeObserver observer);
}
