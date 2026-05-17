using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.Ship.Common;
using Xunit;

namespace Sunfish.Foundation.SecurityPolicy.Tests;

/// <summary>W#37 P1 PR1 — coverage for <see cref="TenantSecurityPolicy"/> default-factory shape per ADR 0068 §1.</summary>
public sealed class TenantSecurityPolicyDefaultsTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");

    [Fact]
    public void DefaultFor_BuildsImmutableProjectionWithNullLastUpdatedBy()
    {
        var now = DateTimeOffset.UtcNow;
        var p = TenantSecurityPolicy.DefaultFor(Tenant, now);

        Assert.Equal(Tenant.Value, p.TenantId.Value);
        Assert.Equal(now, p.LastUpdatedAt);
        Assert.Null(p.LastUpdatedBy);
    }

    [Fact]
    public void DefaultFor_CarriesEachSubPolicyDefault()
    {
        var p = TenantSecurityPolicy.DefaultFor(Tenant, DateTimeOffset.UtcNow);
        Assert.Same(MfaEnrollmentPolicy.Default,      p.Mfa);
        Assert.Same(DeviceAttestationPolicy.Default,  p.DeviceAttestation);
        Assert.Same(AuditRetentionPolicy.Default,     p.AuditRetention);
        Assert.Same(KeyRotationPolicy.Default,        p.KeyRotation);
        Assert.Same(RecoveryContactPolicy.Default,    p.RecoveryContact);
    }

    [Fact]
    public void MfaDefault_CaptainRequiresWebAuthnPlusTotp()
    {
        // Anchor for §1.1.6 — Captain default includes a cognitive-test-free factor.
        var captain = MfaEnrollmentPolicy.Default.RequiredFactorsByRole[ShipRole.Captain];
        Assert.Contains(MfaFactor.WebAuthnPasskey, captain);
        Assert.Contains(MfaFactor.Totp, captain);
    }

    [Fact]
    public void MfaDefault_DivisionOfficerAbsent_TenantConfigurable()
    {
        // §1.1.3 — absent from default = tenant-configurable.
        Assert.False(MfaEnrollmentPolicy.Default.RequiredFactorsByRole.ContainsKey(ShipRole.DivisionOfficer));
    }

    [Fact]
    public void MfaDefault_OODAndEOOWAbsent_WatchDesignations()
    {
        // §1.1.2 — watch designations don't get role-level MFA; underlying base role does.
        Assert.False(MfaEnrollmentPolicy.Default.RequiredFactorsByRole.ContainsKey(ShipRole.OOD));
        // EOOW is in the default map per the ADR — verify presence intentionally
        // (it is configured for Totp; reconfirms the spec).
        Assert.True(MfaEnrollmentPolicy.Default.RequiredFactorsByRole.ContainsKey(ShipRole.EOOW));
    }

    [Fact]
    public void MfaDefault_RecoveryFlowExemptsFromMfaIsFalse()
    {
        // §1.1.1 — recovery restores access, not privileges.
        Assert.False(MfaEnrollmentPolicy.Default.RecoveryFlowExemptsFromMfa);
    }

    [Fact]
    public void DeviceAttestationDefault_ReadAtLeastAsPermissiveAsPrivileged()
    {
        // §1.2.2 invariant must hold on the default value.
        Assert.True(DeviceAttestationPolicy.Default.IsReadAtLeastAsPermissiveAsPrivileged);
    }

    [Fact]
    public void DeviceAttestationDefault_RequiresWatchTransferAttestation()
    {
        Assert.True(DeviceAttestationPolicy.Default.RequireAttestationForWatchTransfer);
    }

    [Fact]
    public void IsReadAtLeastAsPermissiveAsPrivileged_FalseWhenPrivilegedHasTierNotInRead()
    {
        var p = new DeviceAttestationPolicy(
            AcceptedTiersForPrivilegedActions: new[] { AttestationTier.Tpm2, AttestationTier.Fido2HardwareToken },
            AcceptedTiersForReadActions:       new[] { AttestationTier.Tpm2 }, // missing Fido2 → violation
            RequireAttestationForWatchTransfer: true);
        Assert.False(p.IsReadAtLeastAsPermissiveAsPrivileged);
    }

    [Fact]
    public void AuditRetentionDefault_3YearMin7YearMaxCustomPreset()
    {
        Assert.Equal(TimeSpan.FromDays(365 * 3), AuditRetentionPolicy.Default.DefaultMinimumRetentionWindow);
        Assert.Equal(TimeSpan.FromDays(365 * 7), AuditRetentionPolicy.Default.DefaultMaximumRetentionWindow);
        Assert.Equal(RetentionJurisdictionPreset.Custom, AuditRetentionPolicy.Default.JurisdictionPreset);
        Assert.Empty(AuditRetentionPolicy.Default.PerClassOverrides);
    }

    [Fact]
    public void KeyRotationDefault_CaptainCadenceTighter()
    {
        Assert.Equal(TimeSpan.FromDays(90), KeyRotationPolicy.Default.DefaultRotationCadence);
        Assert.Equal(TimeSpan.FromDays(30), KeyRotationPolicy.Default.PerRoleOverrides[ShipRole.Captain]);
    }

    [Fact]
    public void KeyRotationDefault_AutoTriggersIncludeCompromiseAndRecoveryCompleted()
    {
        Assert.Contains(KeyRotationTrigger.CompromiseIndicatorFlagged, KeyRotationPolicy.Default.AutoTriggers);
        Assert.Contains(KeyRotationTrigger.RecoveryCompleted,         KeyRotationPolicy.Default.AutoTriggers);
    }

    [Fact]
    public void RecoveryContactDefault_MinimumOne_Preferred3()
    {
        Assert.Equal(1, RecoveryContactPolicy.Default.MinimumContactCount);
        Assert.Equal(3, RecoveryContactPolicy.Default.PreferredContactCount);
    }

    [Fact]
    public void KeyRotationDefault_AutoTriggers_DoNotIncludeEmergencyOverride()
    {
        // §1.4.2 — emergency rotation requires multi-actor approval and
        // must NOT fire automatically.
        Assert.DoesNotContain(KeyRotationTrigger.EmergencyOverride, KeyRotationPolicy.Default.AutoTriggers);
    }

    [Theory]
    [InlineData(RetentionJurisdictionPreset.Custom)]
    [InlineData(RetentionJurisdictionPreset.HipaaInformedDefault)]
    [InlineData(RetentionJurisdictionPreset.PciDssInformedDefault)]
    [InlineData(RetentionJurisdictionPreset.Soc2InformedDefault)]
    [InlineData(RetentionJurisdictionPreset.GdprInformedDefault)]
    [InlineData(RetentionJurisdictionPreset.EuAiActInformedDefault)]
    public void AuditRetentionPolicy_AllJurisdictionPresets_ConstructValid(RetentionJurisdictionPreset preset)
    {
        // Insurance against an accidental rename / removal breaking the
        // floor validator wiring in PR 2 — every enum value must remain
        // constructable on the policy record.
        var p = AuditRetentionPolicy.Default with { JurisdictionPreset = preset };
        Assert.Equal(preset, p.JurisdictionPreset);
    }

    [Fact]
    public void AttestationEvidence_ConstructsWithEmptyProof_ForDataLayer()
    {
        // §1.2.1: the policy layer ACCEPTS evidence at construction;
        // the verifier (future PR) is the authority on whether empty
        // proof for a hardware tier passes. Data-layer should not
        // pre-reject.
        var e = new AttestationEvidence(AttestationTier.Fido2HardwareToken, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow);
        Assert.True(e.PlatformProof.IsEmpty);
    }

    [Fact]
    public void MfaEnrollmentPolicy_Default_DictionaryIsReadOnly()
    {
        // Defense-in-depth: caller cannot downcast and mutate Default.
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyDictionary<ShipRole, IReadOnlyList<MfaFactor>>>(
            MfaEnrollmentPolicy.Default.RequiredFactorsByRole);
    }
}
