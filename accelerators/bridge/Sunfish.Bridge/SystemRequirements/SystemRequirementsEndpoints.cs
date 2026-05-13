using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.MissionSpace.DependencyInjection;
using CatalogSpecPolicy = Sunfish.Foundation.Catalog.Bundles.SpecPolicy;
using MissionSpecPolicy = Sunfish.Foundation.MissionSpace.SpecPolicy;
using MissionMinimumSpec = Sunfish.Foundation.MissionSpace.MinimumSpec;

namespace Sunfish.Bridge.SystemRequirements;

/// <summary>
/// Bridge route family for the W#56 Phase 1 system-requirements surface
/// (ADR 0063-A1.1). Exposes <c>IMinimumSpecResolver</c> + <c>IInstallForceEnableSurface</c>
/// over HTTP/JSON so React adapter consumers can fetch <c>SystemRequirementsResult</c>
/// across the API boundary.
/// </summary>
public static class SystemRequirementsEndpoints
{
    /// <summary>
    /// Registers the W#56 Phase 1 services: probe singletons via
    /// <see cref="ServiceCollectionExtensions.AddInMemoryMissionSpace"/> + a
    /// server-level <see cref="IMissionEnvelopeProvider"/> built from those probes.
    /// </summary>
    /// <remarks>
    /// Phase 1 uses a server-level (not per-tenant) envelope. Per-tenant envelope
    /// wiring is a future-phase concern once Bridge's tenant-context middleware is
    /// extended to flow envelopes per-tenant per ADR 0031.
    /// </remarks>
    public static IServiceCollection AddBridgeSystemRequirements(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddInMemoryMissionSpace();

        services.TryAddSingleton<IMissionEnvelopeProvider>(sp =>
        {
            var hardware      = sp.GetRequiredService<IDimensionProbe<HardwareCapabilities>>();
            var runtime       = sp.GetRequiredService<IDimensionProbe<RuntimeCapabilities>>();
            var network       = sp.GetRequiredService<IDimensionProbe<NetworkCapabilities>>();
            var user          = sp.GetRequiredService<IDimensionProbe<UserCapabilities>>();
            var edition       = sp.GetRequiredService<IDimensionProbe<EditionCapabilities>>();
            var regulatory    = sp.GetRequiredService<IDimensionProbe<RegulatoryCapabilities>>();
            var trustAnchor   = sp.GetRequiredService<IDimensionProbe<TrustAnchorCapabilities>>();
            var syncState     = sp.GetRequiredService<IDimensionProbe<SyncStateSnapshot>>();
            var versionVector = sp.GetRequiredService<IDimensionProbe<VersionVectorSnapshot>>();
            var formFactor    = sp.GetRequiredService<IDimensionProbe<FormFactorSnapshot>>();
            var time          = sp.GetService<TimeProvider>() ?? TimeProvider.System;

            return new DefaultMissionEnvelopeProvider(
                async ct =>
                {
                    var hw  = await hardware.ProbeAsync(ct).ConfigureAwait(false);
                    var rt  = await runtime.ProbeAsync(ct).ConfigureAwait(false);
                    var net = await network.ProbeAsync(ct).ConfigureAwait(false);
                    var usr = await user.ProbeAsync(ct).ConfigureAwait(false);
                    var ed  = await edition.ProbeAsync(ct).ConfigureAwait(false);
                    var reg = await regulatory.ProbeAsync(ct).ConfigureAwait(false);
                    var tr  = await trustAnchor.ProbeAsync(ct).ConfigureAwait(false);
                    var ss  = await syncState.ProbeAsync(ct).ConfigureAwait(false);
                    var vv  = await versionVector.ProbeAsync(ct).ConfigureAwait(false);
                    var ff  = await formFactor.ProbeAsync(ct).ConfigureAwait(false);
                    return new MissionEnvelope
                    {
                        Hardware      = hw,
                        Runtime       = rt,
                        Network       = net,
                        User          = usr,
                        Edition       = ed,
                        Regulatory    = reg,
                        TrustAnchor   = tr,
                        SyncState     = ss,
                        VersionVector = vv,
                        FormFactor    = ff,
                        SnapshotAt    = time.GetUtcNow(),
                    }.WithComputedHash();
                },
                time);
        });

        return services;
    }

    /// <summary>Wires the W#56 Phase 1 system-requirements route family onto the Bridge.</summary>
    public static IEndpointRouteBuilder MapSystemRequirementsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/system-requirements/{bundleId}", HandleEvaluateAsync);
        app.MapPost("/api/system-requirements/{bundleId}/force-install", HandleForceInstallAsync);
        return app;
    }

    /// <summary>
    /// GET /api/system-requirements/{bundleId}?platform={platformKey}
    /// Resolves the bundle's <c>MinimumSpec</c> from the catalog, evaluates it
    /// against the host's current <c>MissionEnvelope</c>, and returns the
    /// <c>SystemRequirementsResult</c> as JSON.
    /// </summary>
    internal static async Task<IResult> HandleEvaluateAsync(
        string bundleId,
        string? platform,
        IBundleCatalog catalog,
        IMinimumSpecResolver resolver,
        IMissionEnvelopeProvider envelopeProvider,
        CancellationToken ct)
    {
        if (!catalog.TryGet(bundleId, out var manifest))
            return Results.NotFound();

        // Convert stub MinimumSpec (catalog transition window) to canonical.
        // The stub only carries Policy; all 10 per-dimension specs are null →
        // resolver treats each as Unevaluated. Full canonical type migration
        // is scheduled for 2026-08-01 per the stub removal plan.
        var policy = manifest.Requirements is { } stub
            ? (MissionSpecPolicy)(int)stub.Policy
            : MissionSpecPolicy.Recommended;
        var spec = new MissionMinimumSpec { Policy = policy };

        var envelope = await envelopeProvider.GetCurrentAsync(ct).ConfigureAwait(false);
        var result = await resolver.EvaluateAsync(spec, envelope, platform, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// POST /api/system-requirements/{bundleId}/force-install
    /// Records an operator-issued install force-enable per ADR 0063-A1.11.
    /// Caller must be in the operator role (authorization is the host's responsibility).
    /// </summary>
    internal static async Task<IResult> HandleForceInstallAsync(
        string bundleId,
        InstallForceRequest body,
        IInstallForceEnableSurface forceEnableSurface,
        CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Reason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = new[] { "Reason is required (per ADR 0063-A1.11 justification mandate)." },
            });
        }

        await forceEnableSurface.RequestAsync(body, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
}
