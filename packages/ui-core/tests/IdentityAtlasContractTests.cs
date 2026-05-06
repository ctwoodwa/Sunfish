using System;
using System.Linq;
using System.Reflection;
using Sunfish.Foundation.Crypto;
using Sunfish.UICore.Wayfinder;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// W#53 P1b — IIdentityAtlasSurface contract surface tests per ADR
/// 0066 §2.
/// </summary>
public class IdentityAtlasContractTests
{
    [Fact]
    public void IIdentityAtlasSurface_HasFiveMethods()
    {
        var t = typeof(IIdentityAtlasSurface);
        Assert.Equal(5, t.GetMethods().Length);
        Assert.NotNull(t.GetMethod("GetProfileEditAsync"));
        Assert.NotNull(t.GetMethod("GetKeyRotationAsync"));
        Assert.NotNull(t.GetMethod("GetRecoveryContactsAsync"));
        Assert.NotNull(t.GetMethod("GetHistoricalKeysAsync"));
        Assert.NotNull(t.GetMethod("GetActiveTeamOverviewAsync"));
    }

    [Fact]
    public void IdentityProfileEditViewModel_HasRequiredFields()
    {
        var props = typeof(IdentityProfileEditViewModel).GetProperties()
            .Select(p => p.Name).ToHashSet();
        Assert.Contains("Actor", props);
        Assert.Contains("DisplayName", props);
        Assert.Contains("ContactEmail", props);
        Assert.Contains("PhoneNumber", props);
    }

    [Fact]
    public void KeyRotationViewModel_HasRequiredFields()
    {
        var props = typeof(KeyRotationViewModel).GetProperties()
            .Select(p => p.Name).ToHashSet();
        Assert.Contains("Actor", props);
        Assert.Contains("CurrentFingerprint", props);
        Assert.Contains("HistoricalKeyCount", props);
        Assert.Contains("RotationInProgress", props);
        Assert.Contains("RotationWindowExpiry", props);
    }

    [Fact]
    public void RecoveryContact_VerificationStatusIsSyncState()
    {
        var prop = typeof(RecoveryContact).GetProperty("VerificationStatus");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Sunfish.Foundation.UI.SyncState), prop!.PropertyType);
    }

    [Fact]
    public void HistoricalKeyEntry_RotationReason_IsString_Phase1bDeferral()
    {
        // Phase 1b types RotationReason as `string`; ADR 0046-a1 typed
        // `KeyRotationReason` enum is not yet on origin/main.
        var prop = typeof(HistoricalKeyEntry).GetProperty("RotationReason");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void ActiveTeamOverviewViewModel_ActiveTeamId_IsGuid_CycleBreak()
    {
        // Per W#53 P1a cycle-break decision: TeamId? → Guid?.
        // kernel-runtime already references ui-core; reverse dep would
        // form a cycle.
        var prop = typeof(ActiveTeamOverviewViewModel).GetProperty("ActiveTeamId");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Guid?), prop!.PropertyType);
    }

    [Fact]
    public void TeamMembershipEntry_TeamId_IsGuid_CycleBreak()
    {
        var prop = typeof(TeamMembershipEntry).GetProperty("TeamId");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Guid), prop!.PropertyType);
    }

    [Fact]
    public void TeamMembershipEntry_SubkeyFingerprint_IsKeyFingerprint()
    {
        var prop = typeof(TeamMembershipEntry).GetProperty("SubkeyFingerprint");
        Assert.NotNull(prop);
        Assert.Equal(typeof(KeyFingerprint), prop!.PropertyType);
    }

    [Fact]
    public void ViewModels_TimeFields_AreDateTimeOffset_NotNodaTime()
    {
        // Per cohort precedent: DateTimeOffset over NodaTime.Instant.
        Assert.Equal(typeof(DateTimeOffset?),
            typeof(KeyRotationViewModel).GetProperty("RotationWindowExpiry")!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset),
            typeof(RecoveryContact).GetProperty("EnrolledAt")!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset),
            typeof(HistoricalKeyEntry).GetProperty("ActivatedAt")!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset?),
            typeof(HistoricalKeyEntry).GetProperty("RetiredAt")!.PropertyType);
    }
}
