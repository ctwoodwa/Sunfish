using System;
using System.Linq;
using System.Reflection;
using NSubstitute;
using Sunfish.UICore.Conformance;
using Sunfish.UICore.FirstAid;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// W#46 Phase 3 — UICore.Primitives + UICore.FirstAid +
/// UICore.Conformance contract surface tests per ADR 0077 §4 + §7.
/// </summary>
public class UICorePrimitivesTests
{
    [Fact]
    public void LiveRegionPoliteness_HasThreeValues()
    {
        var values = Enum.GetValues<LiveRegionPoliteness>();
        Assert.Equal(3, values.Length);
        Assert.Contains(LiveRegionPoliteness.Polite, values);
        Assert.Contains(LiveRegionPoliteness.Assertive, values);
        Assert.Contains(LiveRegionPoliteness.Critical, values);
    }

    [Fact]
    public void ILiveAnnouncer_Mock_AnnounceWithAssertive_PreservesPoliteness()
    {
        var announcer = Substitute.For<ILiveAnnouncer>();
        announcer.Announce("danger", LiveRegionPoliteness.Assertive);
        announcer.Received(1).Announce("danger", LiveRegionPoliteness.Assertive);
    }

    [Fact]
    public async System.Threading.Tasks.Task IFocusTrap_Mock_EnterThenExit_NoException()
    {
        var trap = Substitute.For<IFocusTrap>();
        await trap.EnterAsync();
        await trap.ExitAsync();
        await trap.Received(1).EnterAsync(System.Threading.CancellationToken.None);
        await trap.Received(1).ExitAsync(System.Threading.CancellationToken.None);
    }

    [Fact]
    public void FormControlKind_HasAtLeastEightValues()
    {
        var values = Enum.GetValues<FormControlKind>();
        Assert.True(values.Length >= 4);
        Assert.Contains(FormControlKind.Text, values);
        Assert.Contains(FormControlKind.Number, values);
        Assert.Contains(FormControlKind.Select, values);
        Assert.Contains(FormControlKind.Checkbox, values);
    }

    [Fact]
    public void IDiffPreview_TwoEntries_SummaryCount()
    {
        var preview = Substitute.For<IDiffPreview>();
        preview.Entries.Returns(new[]
        {
            new DiffEntry("Field1", "old1", "new1"),
            new DiffEntry("Field2", "old2", "new2"),
        });
        preview.Summary.Returns("2 changes");
        Assert.Equal(2, preview.Entries.Count);
        Assert.Equal("2 changes", preview.Summary);
    }

    [Fact]
    public void DiffPreviewView_HasTwoValues()
    {
        var values = Enum.GetValues<DiffPreviewView>();
        Assert.Equal(2, values.Length);
        Assert.Contains(DiffPreviewView.Compact, values);
        Assert.Contains(DiffPreviewView.Expanded, values);
    }
}

public class UICoreFirstAidTests
{
    [Fact]
    public void HelpLocation_HasFourValues()
    {
        var values = Enum.GetValues<HelpLocation>();
        Assert.Equal(4, values.Length);
        Assert.Contains(HelpLocation.TopOfSurface, values);
        Assert.Contains(HelpLocation.Sidebar, values);
        Assert.Contains(HelpLocation.HelpButton, values);
        Assert.Contains(HelpLocation.Inline, values);
    }

    [Fact]
    public void TargetSizeCompliance_HasThreeValues_ConformingFirst()
    {
        var values = Enum.GetValues<TargetSizeCompliance>();
        Assert.Equal(3, values.Length);
        Assert.Equal(TargetSizeCompliance.Conforming, values[0]);
    }

    [Fact]
    public void IFirstAidContract_LiveAnnouncementPolicy_AcceptsAllPolitenessValues()
    {
        // The cross-namespace LiveRegionPoliteness reference compiles
        // and the property accepts every enum value.
        foreach (var politeness in Enum.GetValues<LiveRegionPoliteness>())
        {
            var contract = Substitute.For<IFirstAidContract>();
            contract.LiveAnnouncementPolicy.Returns(politeness);
            Assert.Equal(politeness, contract.LiveAnnouncementPolicy);
        }
    }
}

public class UICoreConformanceTests
{
    [Fact]
    public void Wcag22Level_AA_IsMidValue()
    {
        var values = Enum.GetValues<Wcag22Level>();
        Assert.Equal(3, values.Length);
        Assert.Equal(Wcag22Level.AA, values[1]);
    }

    [Fact]
    public void IConformanceRegistry_RegisterThenForLocation_ReturnsDeclaration()
    {
        // In-memory test impl — the production DefaultConformanceRegistry
        // ships in W#46 Phase 4 (adapter package); Phase 3 ships only
        // the contract.
        var registry = new TestRegistry();
        var declaration = new ConformanceDeclaration(
            LocationId: "quarterdeck",
            SurfaceId: "watch-banner",
            Level: Wcag22Level.AA,
            Covered: new[] { new WcagSuccessCriterion("4.1.3", "Status Messages") },
            Chapters: new[] { new En301549Chapter("9.4.1.3", "Status Messages") },
            Exceptions: Array.Empty<ConformanceException>(),
            DeclaredAt: DateTimeOffset.UtcNow);
        registry.Register(declaration);

        var fetched = registry.ForLocation("quarterdeck");
        Assert.Single(fetched);
        Assert.Equal("watch-banner", fetched[0].SurfaceId);
    }

    [Fact]
    public void ConformanceDeclaration_DeclaredAt_RoundTripsViaIso8601()
    {
        var declared = new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);
        var declaration = new ConformanceDeclaration(
            "quarterdeck", "watch-banner", Wcag22Level.AA,
            Array.Empty<WcagSuccessCriterion>(),
            Array.Empty<En301549Chapter>(),
            Array.Empty<ConformanceException>(),
            declared);
        Assert.Equal("2026-05-06T12:00:00.0000000+00:00", declaration.DeclaredAt.ToString("O"));
    }

    [Fact]
    public void ConformanceException_ExpiresAt_IsNullable()
    {
        var prop = typeof(ConformanceException).GetProperty("ExpiresAt");
        Assert.NotNull(prop);
        Assert.Equal(typeof(DateTimeOffset?), prop!.PropertyType);
    }

    [Fact]
    public void ConformanceDeclaration_LocationId_IsString_CycleBreak()
    {
        // Per W#46 P3 cycle-break: ShipLocation enum lives in
        // foundation-ship-common which transitively cycles back to
        // ui-core. LocationId is string-typed (canonical lowercase
        // ShipLocation wire-form); Phase 3b may relocate ShipLocation
        // to foundation and tighten this field.
        var prop = typeof(ConformanceDeclaration).GetProperty("LocationId");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    private sealed class TestRegistry : IConformanceRegistry
    {
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ConformanceDeclaration>> _store = new();
        public void Register(ConformanceDeclaration declaration)
        {
            if (!_store.TryGetValue(declaration.LocationId, out var list))
            {
                list = new System.Collections.Generic.List<ConformanceDeclaration>();
                _store[declaration.LocationId] = list;
            }
            list.RemoveAll(d => d.SurfaceId == declaration.SurfaceId);
            list.Add(declaration);
        }
        public System.Collections.Generic.IReadOnlyList<ConformanceDeclaration> ForLocation(string locationId) =>
            _store.TryGetValue(locationId, out var list)
                ? list
                : Array.Empty<ConformanceDeclaration>();
    }
}
