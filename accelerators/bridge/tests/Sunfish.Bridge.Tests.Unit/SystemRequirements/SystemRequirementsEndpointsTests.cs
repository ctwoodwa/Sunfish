using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using Sunfish.Bridge.SystemRequirements;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.UI;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.SystemRequirements;

public sealed class SystemRequirementsEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 0, 0, 0, TimeSpan.Zero);

    // ===== GET /api/system-requirements/{bundleId} =====

    [Fact]
    public async Task Evaluate_KnownBundle_Pass_Returns200WithPassVerdict()
    {
        var passResult = MakeResult(OverallVerdict.Pass);
        var catalog = new StubBundleCatalog(("property-management", BuildManifest()));
        var resolver = new StubMinimumSpecResolver(passResult);
        var envelopeProvider = new StubEnvelopeProvider(BuildEnvelope());

        var result = await SystemRequirementsEndpoints.HandleEvaluateAsync(
            "property-management", null, catalog, resolver, envelopeProvider, CancellationToken.None);

        var ok = Assert.IsType<Ok<SystemRequirementsResult>>(result);
        Assert.Equal(OverallVerdict.Pass, ok.Value!.Overall);
    }

    [Fact]
    public async Task Evaluate_KnownBundle_Block_Returns200WithBlockVerdict()
    {
        var blockResult = MakeResult(OverallVerdict.Block);
        var catalog = new StubBundleCatalog(("property-management", BuildManifest()));
        var resolver = new StubMinimumSpecResolver(blockResult);
        var envelopeProvider = new StubEnvelopeProvider(BuildEnvelope());

        var result = await SystemRequirementsEndpoints.HandleEvaluateAsync(
            "property-management", null, catalog, resolver, envelopeProvider, CancellationToken.None);

        var ok = Assert.IsType<Ok<SystemRequirementsResult>>(result);
        Assert.Equal(OverallVerdict.Block, ok.Value!.Overall);
    }

    [Fact]
    public async Task Evaluate_UnknownBundleId_Returns404()
    {
        var catalog = new StubBundleCatalog();
        var resolver = new StubMinimumSpecResolver(MakeResult(OverallVerdict.Pass));
        var envelopeProvider = new StubEnvelopeProvider(BuildEnvelope());

        var result = await SystemRequirementsEndpoints.HandleEvaluateAsync(
            "unknown-bundle", null, catalog, resolver, envelopeProvider, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    // ===== POST /api/system-requirements/{bundleId}/force-install =====

    [Fact]
    public async Task ForceInstall_ValidBody_Returns204AndCallsSurfaceOnce()
    {
        var surface = new StubForceEnableSurface();
        var body = new InstallForceRequest
        {
            OperatorPrincipalId = "op-demo",
            Reason = "Demo override for kitchen-sink environment",
            OverrideTargets = new[] { DimensionChangeKind.Hardware },
            EnvelopeHash = "abc123",
        };

        var result = await SystemRequirementsEndpoints.HandleForceInstallAsync(
            "property-management", body, surface, CancellationToken.None);

        Assert.IsType<NoContent>(result);
        Assert.Equal(1, surface.CallCount);
    }

    // ===== Helpers =====

    private static SystemRequirementsResult MakeResult(OverallVerdict verdict) =>
        new()
        {
            Overall = verdict,
            EvaluatedAt = Now,
        };

    private static BusinessCaseBundleManifest BuildManifest() =>
        new()
        {
            Key = "property-management",
            Name = "Property Management",
            Version = "1.0.0",
        };

    private static MissionEnvelope BuildEnvelope() => new()
    {
        Hardware      = new() { ProbeStatus = ProbeStatus.Healthy },
        User          = new() { IsSignedIn = false, ProbeStatus = ProbeStatus.Healthy },
        Regulatory    = new() { ProbeStatus = ProbeStatus.Healthy },
        Runtime       = new() { ProbeStatus = ProbeStatus.Healthy },
        FormFactor    = new() { ProbeStatus = ProbeStatus.Healthy },
        Edition       = new() { ProbeStatus = ProbeStatus.Healthy },
        Network       = new() { IsOnline = true, ProbeStatus = ProbeStatus.Healthy },
        TrustAnchor   = new() { HasIdentityKey = false, ProbeStatus = ProbeStatus.Healthy },
        SyncState     = new() { State = SyncState.Healthy, ProbeStatus = ProbeStatus.Healthy },
        VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
        SnapshotAt    = Now,
    };

    // ===== Fakes =====

    private sealed class StubBundleCatalog : IBundleCatalog
    {
        private readonly Dictionary<string, BusinessCaseBundleManifest> _bundles;

        public StubBundleCatalog(params (string key, BusinessCaseBundleManifest manifest)[] entries)
        {
            _bundles = new Dictionary<string, BusinessCaseBundleManifest>(entries.Length);
            foreach (var (k, v) in entries) _bundles[k] = v;
        }

        public void Register(BusinessCaseBundleManifest manifest) =>
            _bundles[manifest.Key] = manifest;

        public IReadOnlyList<BusinessCaseBundleManifest> GetBundles() =>
            new List<BusinessCaseBundleManifest>(_bundles.Values);

        public bool TryGet(string key, [NotNullWhen(true)] out BusinessCaseBundleManifest? manifest) =>
            _bundles.TryGetValue(key, out manifest);
    }

    private sealed class StubMinimumSpecResolver : IMinimumSpecResolver
    {
        private readonly SystemRequirementsResult _result;

        public StubMinimumSpecResolver(SystemRequirementsResult result) => _result = result;

        public ValueTask<SystemRequirementsResult> EvaluateAsync(
            Sunfish.Foundation.MissionSpace.MinimumSpec spec,
            MissionEnvelope envelope,
            string? platform = null,
            CancellationToken ct = default)
            => ValueTask.FromResult(_result);

        public void InvalidateCache() { }
    }

    private sealed class StubEnvelopeProvider : IMissionEnvelopeProvider
    {
        private readonly MissionEnvelope _envelope;

        public StubEnvelopeProvider(MissionEnvelope envelope) => _envelope = envelope;

        public ValueTask<MissionEnvelope> GetCurrentAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(_envelope);

        public ValueTask InvalidateAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

        public void Subscribe(IMissionEnvelopeObserver observer) { }

        public void Unsubscribe(IMissionEnvelopeObserver observer) { }
    }

    private sealed class StubForceEnableSurface : IInstallForceEnableSurface
    {
        public int CallCount { get; private set; }

        public ValueTask<InstallForceRecord> RequestAsync(
            InstallForceRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return ValueTask.FromResult(new InstallForceRecord
            {
                OperatorPrincipalId = request.OperatorPrincipalId,
                Reason              = request.Reason,
                OverrideTargets     = request.OverrideTargets,
                EnvelopeHash        = request.EnvelopeHash,
                Platform            = request.Platform,
                RecordedAt          = Now,
            });
        }
    }
}
