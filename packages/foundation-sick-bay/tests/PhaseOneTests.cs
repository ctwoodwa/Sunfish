using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.SickBay;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.SickBay.Tests;

public class PharmacyRecordCountTests
{
    [Fact]
    public void Exact_WithCountBelow3_ReturnsSuppressed()
    {
        Assert.Same(PharmacyRecordCount.Suppressed, PharmacyRecordCount.Exact(0));
        Assert.Same(PharmacyRecordCount.Suppressed, PharmacyRecordCount.Exact(1));
        Assert.Same(PharmacyRecordCount.Suppressed, PharmacyRecordCount.Exact(2));
    }

    [Fact]
    public void Exact_WithCount3_ReturnsValue3NotSuppressed()
    {
        var result = PharmacyRecordCount.Exact(3);
        Assert.False(result.IsSuppressed);
        Assert.Equal(3, result.Value);
    }

    [Fact]
    public void Exact_WithCount100_ReturnsValue100NotSuppressed()
    {
        var result = PharmacyRecordCount.Exact(100);
        Assert.False(result.IsSuppressed);
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Suppressed_Singleton_HasNullValue_AndIsSuppressedTrue()
    {
        Assert.Null(PharmacyRecordCount.Suppressed.Value);
        Assert.True(PharmacyRecordCount.Suppressed.IsSuppressed);
    }

    [Fact]
    public void Exact_WithNegativeCount_TreatedAsZero_ReturnsSuppressed()
    {
        Assert.Same(PharmacyRecordCount.Suppressed, PharmacyRecordCount.Exact(-1));
        Assert.Same(PharmacyRecordCount.Suppressed, PharmacyRecordCount.Exact(int.MinValue));
    }
}

public class FirstAidHintTests
{
    [Fact]
    public void Constructor_RejectsBodyWithLtChar()
    {
        Assert.Throws<ArgumentException>(() =>
            new FirstAidHint("k", "t", "no <script> tag", FirstAidLevel.Info));
    }

    [Fact]
    public void Constructor_RejectsBodyWithGtChar()
    {
        Assert.Throws<ArgumentException>(() =>
            new FirstAidHint("k", "t", "value > 3", FirstAidLevel.Info));
    }

    [Fact]
    public void Constructor_RejectsBodyWithAmpersand()
    {
        Assert.Throws<ArgumentException>(() =>
            new FirstAidHint("k", "t", "&amp; entity", FirstAidLevel.Info));
    }

    [Fact]
    public void Constructor_RejectsBodyWithControlCharBelow0x20()
    {
        Assert.Throws<ArgumentException>(() =>
            new FirstAidHint("k", "t", "tab\there", FirstAidLevel.Info));
        Assert.Throws<ArgumentException>(() =>
            new FirstAidHint("k", "t", "null\0byte", FirstAidLevel.Info));
    }

    [Fact]
    public void Constructor_AcceptsBodyWithNewline0x0A()
    {
        var hint = new FirstAidHint("k", "t", "line one\nline two", FirstAidLevel.Info);
        Assert.Equal("line one\nline two", hint.Body);
    }

    [Fact]
    public void Constructor_AcceptsPlainTextBody()
    {
        var hint = new FirstAidHint(
            "sick-bay.medevac.four-eyes",
            "Four-eyes authorization",
            "Authorization requires a different actor than the requester.",
            FirstAidLevel.Caution);
        Assert.Equal(FirstAidLevel.Caution, hint.Level);
    }
}

public class MedevacStateTransitionTests
{
    [Fact]
    public void StateMachine_DocumentsSixStates()
    {
        var values = Enum.GetValues<MedevacState>();
        Assert.Equal(6, values.Length);
        Assert.Contains(MedevacState.Idle, values);
        Assert.Contains(MedevacState.Requested, values);
        Assert.Contains(MedevacState.PendingAuthorization, values);
        Assert.Contains(MedevacState.Authorized, values);
        Assert.Contains(MedevacState.InProgress, values);
        Assert.Contains(MedevacState.Complete, values);
    }
}

public class ContractSurfaceTests
{
    [Fact]
    public void ISickBayDataProvider_HasRequiredMembers()
    {
        var t = typeof(ISickBayDataProvider);
        Assert.NotNull(t.GetMethod("GetSnapshotAsync"));
        Assert.NotNull(t.GetMethod("SubscribeSnapshotAsync"));
    }

    [Fact]
    public void ISickBayCommandService_HasRequiredMembers()
    {
        var t = typeof(ISickBayCommandService);
        Assert.NotNull(t.GetMethod("TriggerKeyRotationAsync"));
    }

    [Fact]
    public void IMedevacService_HasRequiredMembers()
    {
        var t = typeof(IMedevacService);
        Assert.NotNull(t.GetMethod("GetStateAsync"));
        Assert.NotNull(t.GetMethod("RequestAsync"));
        Assert.NotNull(t.GetMethod("AuthorizeAsync"));
        Assert.NotNull(t.GetMethod("CancelAsync"));
        Assert.NotNull(t.GetMethod("CompleteAsync"));
    }

    [Fact]
    public void IFirstAidSurface_HasRequiredMembers()
    {
        var t = typeof(IFirstAidSurface);
        Assert.NotNull(t.GetMethod("GetContextualHintsAsync"));
    }

    [Fact]
    public void IStretcherBearerPolicy_HasRequiredMembers()
    {
        var t = typeof(IStretcherBearerPolicy);
        Assert.NotNull(t.GetMethod("GetEligibleRespondersAsync"));
    }

    [Fact]
    public void IKeyRotationScheduler_HasRequiredMembers()
    {
        var t = typeof(IKeyRotationScheduler);
        Assert.NotNull(t.GetMethod("ScheduleAsync"));
    }
}

public class AuditEventTypeConstantsTests
{
    [Fact]
    public void AllTenSickBayConstants_PresentAndPascalCase()
    {
        // Cohort precedent (W#46/W#49/W#50/W#55) uses PascalCase wire
        // format — diverging from W#54 hand-off's kebab-case-with-dots.
        Assert.Equal("SickBayPharmacyViewed", AuditEventType.SickBayPharmacyViewed.Value);
        Assert.Equal("SickBayKeyRotationTriggered", AuditEventType.SickBayKeyRotationTriggered.Value);
        Assert.Equal("SickBayLabDiagnosticViewed", AuditEventType.SickBayLabDiagnosticViewed.Value);
        Assert.Equal("SickBayAtmosphereViewed", AuditEventType.SickBayAtmosphereViewed.Value);
        Assert.Equal("SickBayMedevacInitiated", AuditEventType.SickBayMedevacInitiated.Value);
        Assert.Equal("SickBayMedevacAuthorized", AuditEventType.SickBayMedevacAuthorized.Value);
        Assert.Equal("SickBayMedevacCancelled", AuditEventType.SickBayMedevacCancelled.Value);
        Assert.Equal("SickBayMedevacCompleted", AuditEventType.SickBayMedevacCompleted.Value);
        Assert.Equal("SickBayMedevacSelfApprovalRejected", AuditEventType.SickBayMedevacSelfApprovalRejected.Value);
        Assert.Equal("SickBayRecoveryContactManaged", AuditEventType.SickBayRecoveryContactManaged.Value);
    }

    [Fact]
    public void AllSickBayConstants_AreDistinctValues()
    {
        var values = new[]
        {
            AuditEventType.SickBayPharmacyViewed,
            AuditEventType.SickBayKeyRotationTriggered,
            AuditEventType.SickBayLabDiagnosticViewed,
            AuditEventType.SickBayAtmosphereViewed,
            AuditEventType.SickBayMedevacInitiated,
            AuditEventType.SickBayMedevacAuthorized,
            AuditEventType.SickBayMedevacCancelled,
            AuditEventType.SickBayMedevacCompleted,
            AuditEventType.SickBayMedevacSelfApprovalRejected,
            AuditEventType.SickBayRecoveryContactManaged,
        };
        Assert.Equal(values.Length, values.Distinct().Count());
    }
}

public class ShipActionConstantsTests
{
    [Fact]
    public void AllSevenSickBayActions_PresentAndKebabCase()
    {
        Assert.Equal("view-sick-bay", ShipAction.ViewSickBay.Name);
        Assert.Equal("view-pharmacy", ShipAction.ViewPharmacy.Name);
        Assert.Equal("manage-recovery-contacts", ShipAction.ManageRecoveryContacts.Name);
        Assert.Equal("trigger-key-rotation", ShipAction.TriggerKeyRotation.Name);
        Assert.Equal("initiate-medevac", ShipAction.InitiateMedevac.Name);
        Assert.Equal("authorize-medevac", ShipAction.AuthorizeMedevac.Name);
        Assert.Equal("view-first-aid", ShipAction.ViewFirstAid.Name);
    }
}

public class StretcherBearerRoleTests
{
    [Fact]
    public void StretcherBearerRole_IntentionallyDistinctFromShipRole()
    {
        // Per ADR 0082 §3 + §Trust: StretcherBearerRole is INTENTIONALLY
        // not a subset of ShipRole — the narrower enum prevents accidental
        // role-escalation from a notification-routing list to an authority
        // list.
        var values = Enum.GetValues<StretcherBearerRole>();
        Assert.Equal(4, values.Length);
        Assert.Contains(StretcherBearerRole.DCA, values);
        Assert.Contains(StretcherBearerRole.MPA, values);
        Assert.Contains(StretcherBearerRole.CommsOfficer, values);
        Assert.Contains(StretcherBearerRole.SonarOfficer, values);
    }
}

public class OptionsTests
{
    [Fact]
    public void SickBayOptions_RegisterPurpose_OverwritesPriorEntry()
    {
        var opts = new SickBayOptions();
        opts.RegisterPurpose("recovery-key", "Recovery key");
        opts.RegisterPurpose("recovery-key", "Recovery key (renamed)");
        Assert.Equal("Recovery key (renamed)", opts.RegisteredFieldPurposes["recovery-key"]);
    }

    [Fact]
    public void SickBayOptions_RegisteredFieldPurposes_CaseInsensitive()
    {
        var opts = new SickBayOptions();
        opts.RegisterPurpose("recovery-key", "Recovery key");
        Assert.True(opts.RegisteredFieldPurposes.ContainsKey("Recovery-Key"));
        Assert.Equal("Recovery key", opts.RegisteredFieldPurposes["RECOVERY-KEY"]);
    }

    [Fact]
    public void SickBayOptions_FallbackPollingInterval_Defaults60s()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), new SickBayOptions().FallbackPollingInterval);
    }

    [Fact]
    public void AddSunfishSickBay_BindsOptions()
    {
        var services = new ServiceCollection();
        services.AddSunfishSickBay(opts => opts.RegisterPurpose("recovery-key", "Recovery key"));
        var provider = services.BuildServiceProvider();
        var bound = provider.GetRequiredService<IOptions<SickBayOptions>>().Value;
        Assert.True(bound.RegisteredFieldPurposes.ContainsKey("recovery-key"));
    }

    [Fact]
    public void AddSunfishSickBay_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SickBayServiceCollectionExtensions.AddSunfishSickBay(null!));
    }
}
