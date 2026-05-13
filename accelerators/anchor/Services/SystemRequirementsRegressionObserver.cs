using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Singleton that subscribes to <see cref="IMissionEnvelopeProvider"/> and publishes
/// <see cref="DimensionChangeKind"/> regressions to a <see cref="ChannelReader{T}"/>
/// consumed by <c>SystemRequirementsRegressionBanner</c>.
/// Per W#47 Phase 3 / ADR 0063-A1.1.
/// </summary>
/// <remarks>
/// Subscribes on construction (NOT inside OnInitializedAsync) so the subscription
/// survives Blazor component navigations and re-renders.
/// Per ADR 0049: this observer MUST NOT emit audit — regression audit emission
/// is the resolver's responsibility (PostInstallSpecRegression is already a
/// registered AuditEventType from W#41).
/// </remarks>
public sealed class SystemRequirementsRegressionObserver : IMissionEnvelopeObserver
{
    private readonly IMinimumSpecResolver _resolver;
    private readonly Channel<DimensionChangeKind> _channel;

    private MinimumSpec? _spec;
    private string? _platformKey;
    private IReadOnlyList<DimensionEvaluation> _baseline = [];

    public SystemRequirementsRegressionObserver(
        IMissionEnvelopeProvider provider,
        IMinimumSpecResolver resolver)
    {
        _resolver = resolver;
        _channel = Channel.CreateUnbounded<DimensionChangeKind>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        provider.Subscribe(this);
    }

    public ChannelReader<DimensionChangeKind> Regressions => _channel.Reader;

    public void SetContext(MinimumSpec spec, string platformKey)
    {
        _spec = spec;
        _platformKey = platformKey;
    }

    public void UpdateBaseline(IReadOnlyList<DimensionEvaluation> baseline)
        => _baseline = baseline;

    /// <summary>
    /// Directly publishes a regression — called by
    /// <see cref="AnchorMauiSystemRequirementsRenderer"/> when dispatching
    /// <see cref="SystemRequirementsRenderMode.PostInstallRegressionBanner"/> mode.
    /// </summary>
    public void PublishRegression(DimensionChangeKind dimension)
        => _channel.Writer.TryWrite(dimension);

    public async ValueTask OnChangedAsync(EnvelopeChange change, CancellationToken ct = default)
    {
        if (_spec is null || _platformKey is null) return;

        var newResult = await _resolver.EvaluateAsync(_spec, change.Current, _platformKey, ct);

        foreach (var dim in newResult.Dimensions)
        {
            // Per ADR 0063 A1.8 explicit Informational rule — Informational dims never gate.
            if (dim.Policy != DimensionPolicyKind.Required) continue;
            if (dim.Outcome != DimensionPassFail.Fail) continue;

            var prior = _baseline.FirstOrDefault(d => d.Dimension == dim.Dimension);
            if (prior?.Outcome == DimensionPassFail.Pass)
                await _channel.Writer.WriteAsync(dim.Dimension, ct);
        }
    }
}
