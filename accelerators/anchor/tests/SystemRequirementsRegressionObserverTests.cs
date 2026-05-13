using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.UI;
using Xunit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#47 Phase 3 — SystemRequirementsRegressionObserver + regression banner contract tests.
/// The observer is MAUI-free (compiled directly in tests.csproj). The banner component
/// is Razor-generated; test 5 verifies the aria-live attribute via source-file inspection.
/// </summary>
public sealed class SystemRequirementsRegressionObserverTests
{
    // ── Shared test data ─────────────────────────────────────────────────────

    private static readonly MinimumSpec AnySpec = new() { Policy = SpecPolicy.Required };

    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero);

    private static MissionEnvelope AnyEnvelope() => new()
    {
        Hardware = new() { ProbeStatus = ProbeStatus.Healthy, CpuArch = "X64", CpuLogicalCores = 8 },
        User = new() { ProbeStatus = ProbeStatus.Healthy, IsSignedIn = true, PrincipalId = "user-1" },
        Regulatory = new() { ProbeStatus = ProbeStatus.Healthy, JurisdictionCodes = new[] { "US-WA" } },
        Runtime = new() { ProbeStatus = ProbeStatus.Healthy, OsFamily = "Windows", DotnetVersion = "11.0" },
        FormFactor = new() { ProbeStatus = ProbeStatus.Healthy },
        Edition = new() { ProbeStatus = ProbeStatus.Healthy, EditionKey = "anchor-self-host" },
        Network = new() { ProbeStatus = ProbeStatus.Healthy, IsOnline = true },
        TrustAnchor = new() { ProbeStatus = ProbeStatus.Healthy, HasIdentityKey = true },
        SyncState = new() { ProbeStatus = ProbeStatus.Healthy, State = SyncState.Healthy },
        VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
        SnapshotAt = Now,
    };

    private static DimensionEvaluation MakeDim(
        DimensionChangeKind kind,
        DimensionPolicyKind policy,
        DimensionPassFail outcome) =>
        new() { Dimension = kind, Policy = policy, Outcome = outcome };

    private static EnvelopeChange MakeChange(MissionEnvelope current) =>
        new()
        {
            Current = current,
            ChangedDimensions = Array.Empty<DimensionChangeKind>(),
            Severity = EnvelopeChangeSeverity.Warning,
        };

    // ── Test 1: Required+Pass dimension flips to Required+Fail → regression published ──

    [Fact]
    public async Task OnChangedAsync_RequiredPassFlipsToFail_PublishesToChannel()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        var resolver = Substitute.For<IMinimumSpecResolver>();

        var env = AnyEnvelope();

        // After the change, Hardware is Required+Fail
        resolver.EvaluateAsync(
                Arg.Any<MinimumSpec>(),
                Arg.Any<MissionEnvelope>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new SystemRequirementsResult
            {
                Overall = OverallVerdict.Block,
                EvaluatedAt = Now,
                Dimensions =
                [
                    MakeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Fail),
                ],
            });

        var observer = new SystemRequirementsRegressionObserver(provider, resolver);
        observer.SetContext(AnySpec, "windows-desktop");
        // Baseline: Hardware was passing
        observer.UpdateBaseline(
        [
            MakeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
        ]);

        await observer.OnChangedAsync(MakeChange(env));

        var read = observer.Regressions.TryRead(out var dim);
        Assert.True(read, "Expected Hardware regression to be published to channel.");
        Assert.Equal(DimensionChangeKind.Hardware, dim);
    }

    // ── Test 2: Informational dimension flips → no regression published ──────

    [Fact]
    public async Task OnChangedAsync_InformationalDimensionFlips_DoesNotPublish()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        var resolver = Substitute.For<IMinimumSpecResolver>();

        var env = AnyEnvelope();

        // After the change, Network is Informational+Fail (per ADR 0063 A1.8 Informational rule)
        resolver.EvaluateAsync(
                Arg.Any<MinimumSpec>(),
                Arg.Any<MissionEnvelope>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new SystemRequirementsResult
            {
                Overall = OverallVerdict.Pass,
                EvaluatedAt = Now,
                Dimensions =
                [
                    MakeDim(DimensionChangeKind.Network, DimensionPolicyKind.Informational, DimensionPassFail.Fail),
                ],
            });

        var observer = new SystemRequirementsRegressionObserver(provider, resolver);
        observer.SetContext(AnySpec, "windows-desktop");
        observer.UpdateBaseline(
        [
            MakeDim(DimensionChangeKind.Network, DimensionPolicyKind.Informational, DimensionPassFail.Pass),
        ]);

        await observer.OnChangedAsync(MakeChange(env));

        var hasItem = observer.Regressions.TryRead(out _);
        Assert.False(hasItem, "Informational dimension regression must NOT be published.");
    }

    // ── Test 3: PostInstallRegressionBanner dispatch → observer channel + surface fragment ─

    [Fact]
    public async Task Renderer_PostInstallRegressionBannerMode_PopulatesChannelAndFragment()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        var resolver = Substitute.For<IMinimumSpecResolver>();

        var observer = new SystemRequirementsRegressionObserver(provider, resolver);

        var result = new SystemRequirementsResult
        {
            Overall = OverallVerdict.Block,
            EvaluatedAt = Now,
            Dimensions =
            [
                MakeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Fail),
                MakeDim(DimensionChangeKind.Runtime, DimensionPolicyKind.Recommended, DimensionPassFail.Fail),
            ],
        };

        var nav = new FakeNavManager();
        var surface = new FakeSurface(nav);
        var renderer = new AnchorMauiSystemRequirementsRenderer(observer);

        await renderer.RenderAsync(result, surface, SystemRequirementsRenderMode.PostInstallRegressionBanner);

        // RegressionBannerFragment must be set on the surface
        Assert.NotNull(surface.RegressionBannerFragment);
        // Navigation must NOT trigger for banner mode
        Assert.Null(nav.LastUri);

        // Observer channel must contain Hardware (Required+Fail); Recommended is skipped
        var hasHardware = observer.Regressions.TryRead(out var kind);
        Assert.True(hasHardware, "Renderer must publish Required+Fail dimension to observer channel.");
        Assert.Equal(DimensionChangeKind.Hardware, kind);

        // Runtime (Recommended) must NOT be in the channel
        var hasMore = observer.Regressions.TryRead(out _);
        Assert.False(hasMore, "Recommended dimension must not be published by the renderer.");
    }

    // ── Test 4: Subscribe called on construction (NSubstitute) ───────────────

    [Fact]
    public void Constructor_SubscribesToProvider()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        var resolver = Substitute.For<IMinimumSpecResolver>();

        var observer = new SystemRequirementsRegressionObserver(provider, resolver);

        provider.Received(1).Subscribe(observer);
    }

    // ── Test 5: Banner source contains aria-live="assertive" ─────────────────

    [Fact]
    public void RegressionBanner_SourceMarkup_HasAlertRoleForAssertiveAnnouncement()
    {
        var bannerSource = LocateBannerSourceOrFail();
        var markup = File.ReadAllText(bannerSource);

        // WCAG 2.2 SC 4.1.3: role="alert" carries implicit aria-live="assertive" per ARIA 1.2.
        // Explicit aria-live="assertive" was dropped (C3 council amendment) to prevent
        // double-announcement on NVDA+Firefox; role="alert" is the normative mechanism.
        Assert.True(
            markup.Contains("role=\"alert\""),
            $"SystemRequirementsRegressionBanner.razor must declare role=\"alert\" " +
            $"per WCAG 2.2 SC 4.1.3 (assertive live-region for regression announcements). " +
            $"Source: {bannerSource}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string LocateBannerSourceOrFail()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "accelerators", "anchor", "Components",
                "SystemRequirementsRegressionBanner.razor");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate SystemRequirementsRegressionBanner.razor walking up from '{AppContext.BaseDirectory}'.");
    }

    private sealed class FakeSurface : IAnchorSystemRequirementsSurface
    {
        private readonly FakeNavManager _nav;
        public FakeSurface(FakeNavManager nav) => _nav = nav;
        public string Platform => "test-platform";
        public string? BundleId { get; set; }
        public RenderFragment? InlineFragment { get; set; }
        public RenderFragment? RegressionBannerFragment { get; set; }
        public NavigationManager NavigationManager => _nav;
    }

    private sealed class FakeNavManager : NavigationManager
    {
        public string? LastUri { get; private set; }
        public FakeNavManager() => Initialize("http://test/", "http://test/");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastUri = uri;
        protected override void NavigateToCore(string uri, NavigationOptions options) => LastUri = uri;
    }
}
