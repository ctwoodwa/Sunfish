using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Enforcement;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.Ship.Common;
using Xunit;

namespace Sunfish.Foundation.SecurityPolicy.Tests;

/// <summary>W#37 P1 PR 3 — coverage for §4 enforcement contracts + Phase 1 DefaultSecurityPolicyEnforcer.</summary>
public sealed class EnforcementTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly ActorId Actor = new("test-actor-1");

    private static DefaultSecurityPolicyEnforcer Build(
        TenantSecurityPolicy? policy = null,
        IEnumerable<IAttestationVerifier>? verifiers = null)
    {
        var p = policy ?? TenantSecurityPolicy.DefaultFor(Tenant, DateTimeOffset.UtcNow);
        return new DefaultSecurityPolicyEnforcer(
            policyLoader: (_, _) => new ValueTask<TenantSecurityPolicy>(p),
            verifiers: verifiers);
    }

    // --- PolicyCheckResult factory ---

    [Fact]
    public void Compliant_FactoryReturnsCompliant()
    {
        var r = PolicyCheckResult.Compliant();
        Assert.True(r.IsCompliant);
        Assert.Null(r.Violation);
    }

    [Fact]
    public void ViolationResult_RequiresAccessibleMessageAndSuggestion()
    {
        Assert.Throws<ArgumentException>(() =>
            PolicyCheckResult.ViolationResult(
                PolicyViolationKind.MfaEnrollmentRequired, "", "suggest"));
        Assert.Throws<ArgumentException>(() =>
            PolicyCheckResult.ViolationResult(
                PolicyViolationKind.MfaEnrollmentRequired, "msg", "  "));
    }

    [Fact]
    public void ViolationResult_PopulatesAllFields()
    {
        var r = PolicyCheckResult.ViolationResult(
            PolicyViolationKind.DeviceAttestationRequired,
            "Hardware tier required.",
            "Enroll a TPM 2.0 / Apple Secure Enclave / FIDO2 device.",
            TimeSpan.FromHours(2));
        Assert.False(r.IsCompliant);
        Assert.Equal(PolicyViolationKind.DeviceAttestationRequired, r.Violation);
        Assert.Equal(TimeSpan.FromHours(2), r.GracePeriodRemaining);
    }

    [Fact]
    public void PolicyCheckResult_PropertiesAreInitPrivate_FactoryOnlyConstruction()
    {
        var prop = typeof(PolicyCheckResult).GetProperty(nameof(PolicyCheckResult.AccessibleMessage))!;
        var setter = prop.SetMethod!;
        Assert.True(setter.IsPrivate, "AccessibleMessage init setter must be private to enforce factory-only construction.");
    }

    // --- DefaultSecurityPolicyEnforcer: device attestation ---

    [Fact]
    public async Task DeviceAttestation_SoftwareSandbox_AcceptedForReadActions()
    {
        // Default policy: Read accepts SoftwareSandbox + hardware tiers.
        var enforcer = Build();
        var evidence = new AttestationEvidence(
            AttestationTier.SoftwareSandbox,
            ReadOnlyMemory<byte>.Empty,
            DateTimeOffset.UtcNow);
        var r = await enforcer.CheckDeviceAttestationAsync(Tenant, Actor, evidence, isPrivilegedAction: false);
        Assert.True(r.IsCompliant);
    }

    [Fact]
    public async Task DeviceAttestation_SoftwareSandbox_RejectedForPrivilegedActions()
    {
        // Default policy: Privileged actions exclude SoftwareSandbox.
        var enforcer = Build();
        var evidence = new AttestationEvidence(
            AttestationTier.SoftwareSandbox,
            ReadOnlyMemory<byte>.Empty,
            DateTimeOffset.UtcNow);
        var r = await enforcer.CheckDeviceAttestationAsync(Tenant, Actor, evidence, isPrivilegedAction: true);
        Assert.False(r.IsCompliant);
        Assert.Equal(PolicyViolationKind.DeviceAttestationBelowTier, r.Violation);
    }

    [Fact]
    public async Task DeviceAttestation_HardwareTier_FailsClosedWithoutVerifier()
    {
        // ADR 0068 §4.3: hardware-tier evidence MUST go through a registered
        // verifier; absence fails closed. Phase 1 ships zero verifiers.
        var enforcer = Build();
        var evidence = new AttestationEvidence(
            AttestationTier.Tpm2,
            new byte[] { 1, 2, 3, 4 },
            DateTimeOffset.UtcNow);
        var r = await enforcer.CheckDeviceAttestationAsync(Tenant, Actor, evidence, isPrivilegedAction: true);
        Assert.False(r.IsCompliant);
        Assert.Equal(PolicyViolationKind.DeviceAttestationRequired, r.Violation);
    }

    [Fact]
    public async Task DeviceAttestation_HardwareTier_AcceptedWhenVerifierRegisteredAndProofPasses()
    {
        var verifier = new StubVerifier(AttestationTier.Tpm2, isVerified: true);
        var enforcer = Build(verifiers: new[] { (IAttestationVerifier)verifier });
        var evidence = new AttestationEvidence(
            AttestationTier.Tpm2,
            new byte[] { 1, 2, 3 },
            DateTimeOffset.UtcNow);
        var r = await enforcer.CheckDeviceAttestationAsync(Tenant, Actor, evidence, isPrivilegedAction: true);
        Assert.True(r.IsCompliant);
    }

    [Fact]
    public async Task DeviceAttestation_HardwareTier_RejectedWhenVerifierFails_FailureReasonNotInAccessibleMessage()
    {
        // FailureReason MUST NOT leak into the UI-bound AccessibleMessage —
        // it can contain device internals (PCR mismatches, byte ranges).
        // Verifier-supplied detail belongs in the audit-payload only.
        var verifier = new StubVerifier(AttestationTier.Tpm2, isVerified: false,
            failureReason: "TPM PCR_7 mismatch (expected: 0xAAAA, actual: 0xBBBB)");
        var enforcer = Build(verifiers: new[] { (IAttestationVerifier)verifier });
        var evidence = new AttestationEvidence(
            AttestationTier.Tpm2,
            new byte[] { 1, 2, 3 },
            DateTimeOffset.UtcNow);
        var r = await enforcer.CheckDeviceAttestationAsync(Tenant, Actor, evidence, isPrivilegedAction: true);
        Assert.False(r.IsCompliant);
        Assert.Equal(PolicyViolationKind.DeviceAttestationRequired, r.Violation);
        Assert.DoesNotContain("PCR_7", r.AccessibleMessage);
        Assert.DoesNotContain("0xAAAA", r.AccessibleMessage);
    }

    [Fact]
    public void IsCompliant_IsComputed_FromViolation_NotInitSetter()
    {
        // xo-council B2 precedent — caller cannot construct
        // (IsCompliant=true, Violation=MfaEnrollmentRequired).
        // S1 tightening (private init) makes the bypass a compile-time
        // error; this asserts the factory path yields the correct
        // IsCompliant=false result.
        var r = PolicyCheckResult.ViolationResult(
            PolicyViolationKind.MfaEnrollmentRequired, "msg", "suggest");
        Assert.False(r.IsCompliant);
    }

    [Fact]
    public void DuplicateTierVerifiers_ThrowsAtConstruction()
    {
        var v1 = new StubVerifier(AttestationTier.Tpm2, isVerified: true);
        var v2 = new StubVerifier(AttestationTier.Tpm2, isVerified: false);
        Assert.Throws<ArgumentException>(() =>
            new DefaultSecurityPolicyEnforcer(
                policyLoader: (_, _) => new ValueTask<TenantSecurityPolicy>(TenantSecurityPolicy.DefaultFor(Tenant, DateTimeOffset.UtcNow)),
                verifiers:    new[] { (IAttestationVerifier)v1, v2 }));
    }

    // --- Phase 1 stubs ---

    [Fact]
    public async Task Mfa_Phase1_ReturnsCompliantUnconditionally()
    {
        var enforcer = Build();
        var r = await enforcer.CheckMfaComplianceAsync(Tenant, Actor, ShipRole.Captain);
        Assert.True(r.IsCompliant);
    }

    [Fact]
    public async Task KeyRotation_Phase1_ReturnsCurrent()
    {
        var enforcer = Build();
        var s = await enforcer.GetKeyRotationStatusAsync(Tenant, Actor, ShipRole.Captain);
        Assert.Equal(KeyRotationStatus.Current, s);
    }

    [Fact]
    public async Task RecoveryContact_Phase1_ReturnsCompliant()
    {
        var enforcer = Build();
        var s = await enforcer.GetRecoveryContactComplianceAsync(Tenant, Actor);
        Assert.Equal(RecoveryContactComplianceStatus.Compliant, s);
    }

    // --- Test helpers ---

    private sealed class StubVerifier : IAttestationVerifier
    {
        private readonly bool _isVerified;
        private readonly string? _failureReason;

        public StubVerifier(AttestationTier tier, bool isVerified, string? failureReason = null)
        {
            SupportedTier = tier;
            _isVerified = isVerified;
            _failureReason = failureReason;
        }

        public AttestationTier SupportedTier { get; }

        public ValueTask<AttestationVerificationResult> VerifyAsync(
            ReadOnlyMemory<byte> platformProof, DateTimeOffset evidenceAt, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new AttestationVerificationResult(_isVerified, SupportedTier, _failureReason));
    }
}
