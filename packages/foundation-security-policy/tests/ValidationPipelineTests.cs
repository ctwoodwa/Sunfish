using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.SecurityPolicy.Validation;
using Sunfish.Foundation.SecurityPolicy.Validation.Validators;
using Sunfish.Foundation.Ship.Common;
using Xunit;

namespace Sunfish.Foundation.SecurityPolicy.Tests;

/// <summary>W#37 P1 PR 2 — coverage for §2 validation pipeline (schema + consistency + floor).</summary>
public sealed class ValidationPipelineTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly ActorId Actor = new("test-actor-1");
    private static readonly SecurityPolicyValidationContext Ctx =
        new(Tenant, Actor, ShipRole.Captain);

    private static TenantSecurityPolicy Baseline()
        => TenantSecurityPolicy.DefaultFor(Tenant, DateTimeOffset.UtcNow);

    // --- Schema ---

    [Fact]
    public async Task SchemaValidator_RejectsZeroRetentionWindow()
    {
        var sut = new SchemaValidator();
        var bad = Baseline() with
        {
            AuditRetention = AuditRetentionPolicy.Default with { DefaultMinimumRetentionWindow = TimeSpan.Zero }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, f => f.Code == "SCHEMA_RETENTION_MIN_NONPOSITIVE");
    }

    [Fact]
    public async Task SchemaValidator_RejectsNegativeMfaGrace()
    {
        var sut = new SchemaValidator();
        var bad = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { EnrollmentGracePeriod = TimeSpan.FromDays(-1) }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "SCHEMA_MFA_GRACE_NEGATIVE");
    }

    [Fact]
    public async Task SchemaValidator_AcceptsDefault()
    {
        var sut = new SchemaValidator();
        var result = await sut.ValidateAsync(Baseline(), Baseline(), Ctx);
        Assert.True(result.IsValid);
    }

    // --- Consistency ---

    [Fact]
    public async Task ConsistencyValidator_RejectsReadStricterThanPrivileged()
    {
        var sut = new ConsistencyValidator();
        var bad = Baseline() with
        {
            DeviceAttestation = new DeviceAttestationPolicy(
                AcceptedTiersForPrivilegedActions: new[] { AttestationTier.Tpm2, AttestationTier.Fido2HardwareToken },
                AcceptedTiersForReadActions:       new[] { AttestationTier.Tpm2 }, // missing Fido2
                RequireAttestationForWatchTransfer: true)
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "CONSISTENCY_READ_STRICTER_THAN_PRIVILEGED");
    }

    [Fact]
    public async Task ConsistencyValidator_RejectsCompromiseTriggerRemoval()
    {
        var sut = new ConsistencyValidator();
        var bad = Baseline() with
        {
            KeyRotation = KeyRotationPolicy.Default with
            {
                AutoTriggers = KeyRotationPolicy.Default.AutoTriggers
                    .Where(t => t != KeyRotationTrigger.CompromiseIndicatorFlagged)
                    .ToArray()
            }
        };
        // current = baseline (DOES contain it); proposed (bad) removed it.
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "CONSISTENCY_COMPROMISE_TRIGGER_REMOVED");
    }

    [Fact]
    public async Task ConsistencyValidator_RejectsGraceGteCadence()
    {
        var sut = new ConsistencyValidator();
        var bad = Baseline() with
        {
            KeyRotation = KeyRotationPolicy.Default with
            {
                DefaultRotationCadence = TimeSpan.FromDays(7),
                RotationGracePeriod    = TimeSpan.FromDays(7),
            }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "CONSISTENCY_KEY_GRACE_GTE_CADENCE");
    }

    [Fact]
    public async Task ConsistencyValidator_AcceptsDefault()
    {
        var sut = new ConsistencyValidator();
        var result = await sut.ValidateAsync(Baseline(), Baseline(), Ctx);
        Assert.True(result.IsValid);
    }

    // --- Floor ---

    [Theory]
    [InlineData(ShipRole.Captain)]
    [InlineData(ShipRole.XO)]
    [InlineData(ShipRole.EngineerOfficer)]
    public async Task FloorPolicyValidator_RejectsHighPrivRoleWithSmsOnly(ShipRole role)
    {
        var sut = new FloorPolicyValidator();
        var dict = new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>(
            MfaEnrollmentPolicy.Default.RequiredFactorsByRole)
        {
            [role] = new[] { MfaFactor.Sms },
        };
        var bad = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { RequiredFactorsByRole = dict }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_HIGH_PRIV_LOW_ASSURANCE_ONLY" && f.Message.Contains(role.ToString()));
    }

    [Theory]
    [InlineData(ShipRole.Captain)]
    [InlineData(ShipRole.XO)]
    [InlineData(ShipRole.EngineerOfficer)]
    public async Task FloorPolicyValidator_RejectsHighPrivRoleWithEmailOnly(ShipRole role)
    {
        var sut = new FloorPolicyValidator();
        var dict = new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>(
            MfaEnrollmentPolicy.Default.RequiredFactorsByRole)
        {
            [role] = new[] { MfaFactor.Email },
        };
        var bad = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { RequiredFactorsByRole = dict }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_HIGH_PRIV_LOW_ASSURANCE_ONLY" && f.Message.Contains(role.ToString()));
    }

    [Theory]
    [InlineData(ShipRole.Captain)]
    [InlineData(ShipRole.XO)]
    [InlineData(ShipRole.EngineerOfficer)]
    public async Task FloorPolicyValidator_RejectsHighPrivRoleWithEmailPlusSms(ShipRole role)
    {
        // Combined-low-assurance gap: [Email, Sms] has no acceptable-
        // assurance factor. Per ADR §1.1.4 intent.
        var sut = new FloorPolicyValidator();
        var dict = new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>(
            MfaEnrollmentPolicy.Default.RequiredFactorsByRole)
        {
            [role] = new[] { MfaFactor.Email, MfaFactor.Sms },
        };
        var bad = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { RequiredFactorsByRole = dict }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_HIGH_PRIV_LOW_ASSURANCE_ONLY" && f.Message.Contains(role.ToString()));
    }

    [Theory]
    [InlineData(ShipRole.Captain)]
    [InlineData(ShipRole.XO)]
    [InlineData(ShipRole.EngineerOfficer)]
    public async Task FloorPolicyValidator_AcceptsHighPrivRoleWithTotpOnly(ShipRole role)
    {
        // Totp is acceptable-assurance per ADR §1.1.4 — only Email
        // and Sms are forbidden. WCAG 3.3.8 Warning (rule f) is a
        // separate Warning, not an Error.
        var sut = new FloorPolicyValidator();
        var dict = new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>(
            MfaEnrollmentPolicy.Default.RequiredFactorsByRole)
        {
            [role] = new[] { MfaFactor.Totp },
        };
        var good = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { RequiredFactorsByRole = dict }
        };
        var result = await sut.ValidateAsync(good, Baseline(), Ctx);
        Assert.DoesNotContain(result.Findings, f => f.Code == "FLOOR_HIGH_PRIV_LOW_ASSURANCE_ONLY" && f.Message.Contains(role.ToString()));
    }

    [Theory]
    [InlineData(ShipRole.Captain)]
    [InlineData(ShipRole.XO)]
    [InlineData(ShipRole.EngineerOfficer)]
    public async Task FloorPolicyValidator_AcceptsHighPrivRoleWithWebAuthnPlusTotp(ShipRole role)
    {
        var sut = new FloorPolicyValidator();
        var dict = new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>(
            MfaEnrollmentPolicy.Default.RequiredFactorsByRole)
        {
            [role] = new[] { MfaFactor.WebAuthnPasskey, MfaFactor.Totp },
        };
        var good = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { RequiredFactorsByRole = dict }
        };
        var result = await sut.ValidateAsync(good, Baseline(), Ctx);
        Assert.DoesNotContain(result.Findings, f => f.Code == "FLOOR_HIGH_PRIV_LOW_ASSURANCE_ONLY" && f.Message.Contains(role.ToString()));
    }

    [Fact]
    public async Task FloorPolicyValidator_RejectsCompromiseTriggerMissing()
    {
        var sut = new FloorPolicyValidator();
        var bad = Baseline() with
        {
            KeyRotation = KeyRotationPolicy.Default with
            {
                AutoTriggers = new[] { KeyRotationTrigger.CadenceExpired }, // no CompromiseIndicatorFlagged
            }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_COMPROMISE_TRIGGER_REQUIRED");
    }

    [Fact]
    public async Task FloorPolicyValidator_DoesNotDuplicateSchemaMinContactCheck()
    {
        // xo-council B3 — schema is the owner of MinimumContactCount < 1
        // (it's structural nonsense, not a jurisdictional floor). Floor
        // validator MUST NOT emit a duplicate finding for the same issue.
        var sut = new FloorPolicyValidator();
        var bad = Baseline() with
        {
            RecoveryContact = RecoveryContactPolicy.Default with { MinimumContactCount = 0 }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.DoesNotContain(result.Findings, f => f.Code == "FLOOR_RECOVERY_MIN_LT_ONE");
    }

    [Fact]
    public async Task FloorPolicyValidator_HipaaPreset_FloorsIdentitySecurityConfigAt6Years()
    {
        var sut = new FloorPolicyValidator();
        var bad = Baseline() with
        {
            AuditRetention = AuditRetentionPolicy.Default with
            {
                JurisdictionPreset            = RetentionJurisdictionPreset.HipaaInformedDefault,
                DefaultMinimumRetentionWindow = TimeSpan.FromDays(365), // 1y, below 6y floor
            }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        // xo-council B4 — class-distinguishing codes per (preset, class) tuple.
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_HIPAA_RETENTION_LT_6YR_IDENTITY");
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_HIPAA_RETENTION_LT_6YR_SECURITY");
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_HIPAA_RETENTION_LT_6YR_CONFIGURATION");
    }

    [Fact]
    public async Task FloorPolicyValidator_PciDssPreset_FloorsFinancialSecurityAt12Months()
    {
        var sut = new FloorPolicyValidator();
        var bad = Baseline() with
        {
            AuditRetention = AuditRetentionPolicy.Default with
            {
                JurisdictionPreset            = RetentionJurisdictionPreset.PciDssInformedDefault,
                DefaultMinimumRetentionWindow = TimeSpan.FromDays(30), // 30d, below 12mo floor
            }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_PCIDSS_RETENTION_LT_12MO_FINANCIAL");
        Assert.Contains(result.Findings, f => f.Code == "FLOOR_PCIDSS_RETENTION_LT_12MO_SECURITY");
    }

    [Fact]
    public async Task FloorPolicyValidator_WcagWarningForCognitiveOnlyRole()
    {
        var sut = new FloorPolicyValidator();
        // EngineerOfficer default is Totp-only; baseline already triggers the WCAG warning.
        var result = await sut.ValidateAsync(Baseline(), Baseline(), Ctx);
        Assert.Contains(result.Findings, f =>
            f.Code == "FLOOR_WCAG_338_COGNITIVE_ONLY"
            && f.Severity == SecurityPolicyValidationSeverity.Warning);
        // The default is valid overall (no Error) — WCAG warning doesn't block.
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task FloorPolicyValidator_NoWarning_WhenRoleHasNonCognitiveFactor()
    {
        var sut = new FloorPolicyValidator();
        // Captain default has WebAuthnPasskey — no warning for Captain.
        var result = await sut.ValidateAsync(Baseline(), Baseline(), Ctx);
        Assert.DoesNotContain(result.Findings, f =>
            f.Code == "FLOOR_WCAG_338_COGNITIVE_ONLY"
            && f.Message.Contains("Captain"));
    }

    [Fact]
    public async Task SchemaValidator_RejectsUndefinedKeyRotationTrigger()
    {
        var sut = new SchemaValidator();
        var bad = Baseline() with
        {
            KeyRotation = KeyRotationPolicy.Default with
            {
                AutoTriggers = new[] { (KeyRotationTrigger)999 },
            }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "SCHEMA_KEY_TRIGGER_UNDEFINED");
    }

    // --- Finding factory WCAG enforcement ---

    [Fact]
    public void Finding_ErrorFactory_RequiresAccessibleMessageAndSuggestion()
    {
        Assert.Throws<ArgumentException>(() =>
            SecurityPolicyValidationFinding.Error("CODE", "", "suggest"));
        Assert.Throws<ArgumentException>(() =>
            SecurityPolicyValidationFinding.Error("CODE", "msg", ""));
    }

    [Fact]
    public void Finding_WarningFactory_RequiresAccessibleMessageAndSuggestion()
    {
        Assert.Throws<ArgumentException>(() =>
            SecurityPolicyValidationFinding.Warning("CODE", "msg", "  "));
    }

    [Fact]
    public void Finding_ErrorFactory_RequiresNonEmptyCode()
    {
        // xo-council R5 — Code symmetry with Message + Suggestion.
        Assert.Throws<ArgumentException>(() =>
            SecurityPolicyValidationFinding.Error("", "msg", "suggest"));
        Assert.Throws<ArgumentException>(() =>
            SecurityPolicyValidationFinding.Warning("  ", "msg", "suggest"));
    }

    // --- B2: IsValid as computed property ---

    [Fact]
    public void Result_IsValid_ComputedFromFindings_NotConstructorArg()
    {
        // xo-council B2 — caller cannot construct (IsValid=true, Findings=[error]).
        var result = new SecurityPolicyValidationResult(new[]
        {
            SecurityPolicyValidationFinding.Error("CODE", "msg", "suggest"),
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Result_Empty_IsValid()
    {
        Assert.True(SecurityPolicyValidationResult.Empty.IsValid);
        Assert.Empty(SecurityPolicyValidationResult.Empty.Findings);
    }

    // --- B6 additional Enum.IsDefined checks ---

    [Fact]
    public async Task SchemaValidator_RejectsUndefinedShipRoleInMfaDict()
    {
        var sut = new SchemaValidator();
        var dict = new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>
        {
            [(ShipRole)999] = new[] { MfaFactor.Totp },
        };
        var bad = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { RequiredFactorsByRole = dict }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "SCHEMA_MFA_ROLE_UNDEFINED");
    }

    [Fact]
    public async Task SchemaValidator_RejectsUndefinedShipRoleInKeyOverrides()
    {
        var sut = new SchemaValidator();
        var bad = Baseline() with
        {
            KeyRotation = KeyRotationPolicy.Default with
            {
                PerRoleOverrides = new Dictionary<ShipRole, TimeSpan>
                {
                    [(ShipRole)999] = TimeSpan.FromDays(30),
                }
            }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "SCHEMA_KEY_ROLE_UNDEFINED");
    }

    [Fact]
    public async Task SchemaValidator_RejectsUndefinedAttestationTier()
    {
        var sut = new SchemaValidator();
        var bad = Baseline() with
        {
            DeviceAttestation = new DeviceAttestationPolicy(
                AcceptedTiersForPrivilegedActions: new[] { (AttestationTier)999 },
                AcceptedTiersForReadActions:       new[] { (AttestationTier)999 },
                RequireAttestationForWatchTransfer: true)
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "SCHEMA_ATTESTATION_TIER_UNDEFINED");
    }

    [Fact]
    public async Task SchemaValidator_RejectsUndefinedAuditEventClassKey()
    {
        var sut = new SchemaValidator();
        var bad = Baseline() with
        {
            AuditRetention = AuditRetentionPolicy.Default with
            {
                PerClassOverrides = new Dictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>
                {
                    [(AuditEventClass)999] = (TimeSpan.FromDays(365), TimeSpan.FromDays(365 * 7)),
                }
            }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.Contains(result.Findings, f => f.Code == "SCHEMA_RETENTION_CLASS_UNDEFINED");
    }

    // --- B1: empty factor list rejection (vacuous All() = true edge case) ---

    [Theory]
    [InlineData(ShipRole.Captain)]
    [InlineData(ShipRole.XO)]
    [InlineData(ShipRole.EngineerOfficer)]
    public async Task FloorPolicyValidator_TreatsEmptyFactorList_AsNotConfigured_NotLowAssurance(ShipRole role)
    {
        // Empty factor list is treated as "not configured" (early-
        // continue in the rule (b) loop) — same as the absent-key case.
        // If empty-list should error, that's a new rule beyond ADR
        // §1.1.4 wording; raise XO clarification first.
        var sut = new FloorPolicyValidator();
        var dict = new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>(
            MfaEnrollmentPolicy.Default.RequiredFactorsByRole)
        {
            [role] = Array.Empty<MfaFactor>(),
        };
        var bad = Baseline() with
        {
            Mfa = MfaEnrollmentPolicy.Default with { RequiredFactorsByRole = dict }
        };
        var result = await sut.ValidateAsync(bad, Baseline(), Ctx);
        Assert.DoesNotContain(result.Findings, f =>
            f.Code == "FLOOR_HIGH_PRIV_LOW_ASSURANCE_ONLY" && f.Message.Contains(role.ToString()));
    }

    [Fact]
    public async Task ConsistencyValidator_AcceptsReadSupersetOfPrivileged()
    {
        // xo-council R4 — positive case for IsReadAtLeastAsPermissiveAsPrivileged.
        var sut = new ConsistencyValidator();
        var ok = Baseline() with
        {
            DeviceAttestation = new DeviceAttestationPolicy(
                AcceptedTiersForPrivilegedActions: new[] { AttestationTier.Tpm2 },
                AcceptedTiersForReadActions:       new[] { AttestationTier.Tpm2, AttestationTier.SoftwareSandbox },
                RequireAttestationForWatchTransfer: true)
        };
        var result = await sut.ValidateAsync(ok, Baseline(), Ctx);
        Assert.True(result.IsValid);
    }
}
