using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Foundation.Taxonomy.Seeds;
using Sunfish.Foundation.Taxonomy.Services;
using Sunfish.Kernel.Signatures.Models;
using Sunfish.Kernel.Signatures.Services;
using Xunit;

namespace Sunfish.Kernel.Signatures.Tests;

/// <summary>
/// W#21 Phase 4 — scope validation per ADR 0054 amendment A7. Exercises
/// the validator against the canonical <c>Sunfish.Signature.Scopes@1.0.0</c>
/// taxonomy seed (W#31).
/// </summary>
public sealed class ScopeValidationTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly ActorId TestSigner = new("alice@example.com");

    private static async Task<(InMemorySignatureScopeValidator validator, InMemoryTaxonomyRegistry registry)> NewValidatorAsync()
    {
        // Build a registry seeded with the canonical signature-scopes
        // package + bind a resolver to it via the public registry API.
        var registry = new InMemoryTaxonomyRegistry();
        var pkg = TaxonomyCorePackages.SunfishSignatureScopes;
        await registry.RegisterCorePackageAsync(TestTenant, pkg, default);
        var resolver = new InMemoryTaxonomyResolver(registry);
        return (new InMemorySignatureScopeValidator(resolver), registry);
    }

    private static TaxonomyClassification ScopeFor(string code) => new()
    {
        Definition = InMemorySignatureScopeValidator.DefaultTaxonomy,
        Version = TaxonomyVersion.V1_0_0,
        Code = code,
    };

    [Fact]
    public async Task Validate_KnownActiveScope_Passes()
    {
        var (validator, _) = await NewValidatorAsync();
        var result = await validator.ValidateAsync(TestTenant, new[] { ScopeFor("lease-execution") }, default);

        Assert.True(result.Passed);
        Assert.Null(result.RejectedAt);
    }

    [Fact]
    public async Task Validate_MultipleKnownActiveScopes_Passes()
    {
        var (validator, _) = await NewValidatorAsync();
        var scopes = new[]
        {
            ScopeFor("lease-execution"),
            ScopeFor("consent-background-check"),
            ScopeFor("payment-authorization"),
        };
        var result = await validator.ValidateAsync(TestTenant, scopes, default);

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Validate_EmptyScope_Fails()
    {
        var (validator, _) = await NewValidatorAsync();
        var result = await validator.ValidateAsync(TestTenant, Array.Empty<TaxonomyClassification>(), default);

        Assert.False(result.Passed);
        Assert.Equal(ScopeValidationFailure.EmptyScope, result.FailedBecause);
    }

    [Fact]
    public async Task Validate_UnknownNode_Fails()
    {
        var (validator, _) = await NewValidatorAsync();
        var unknown = ScopeFor("nonexistent-scope-code");

        var result = await validator.ValidateAsync(TestTenant, new[] { unknown }, default);

        Assert.False(result.Passed);
        Assert.Equal(ScopeValidationFailure.UnknownNode, result.FailedBecause);
        Assert.Equal(unknown, result.RejectedAt);
    }

    [Fact]
    public async Task Validate_OutOfTaxonomy_Fails()
    {
        var (validator, _) = await NewValidatorAsync();
        // A classification referencing a different taxonomy entirely
        // (e.g., Sunfish.Vendor.Specialties from W#18 Phase 6).
        var alien = new TaxonomyClassification
        {
            Definition = new TaxonomyDefinitionId("Sunfish", "Vendor", "Specialties"),
            Version = TaxonomyVersion.V1_0_0,
            Code = "plumbing",
        };

        var result = await validator.ValidateAsync(TestTenant, new[] { alien }, default);

        Assert.False(result.Passed);
        Assert.Equal(ScopeValidationFailure.OutOfTaxonomy, result.FailedBecause);
        Assert.Equal(alien, result.RejectedAt);
    }

    [Fact]
    public async Task Validate_TombstonedNode_Fails()
    {
        var (validator, registry) = await NewValidatorAsync();
        // Tombstone a node via the public registry API.
        await registry.TombstoneNodeAsync(
            tenantId: TestTenant,
            nodeId: new TaxonomyNodeId(InMemorySignatureScopeValidator.DefaultTaxonomy, "lease-execution"),
            version: TaxonomyVersion.V1_0_0,
            deprecationReason: "Test tombstone for ScopeValidator coverage.",
            successorCode: null,
            tombstonedBy: ActorId.Sunfish,
            ct: default);

        var result = await validator.ValidateAsync(TestTenant, new[] { ScopeFor("lease-execution") }, default);

        Assert.False(result.Passed);
        Assert.Equal(ScopeValidationFailure.TombstonedNode, result.FailedBecause);
    }

    [Fact]
    public async Task Validate_StopsAtFirstFailure()
    {
        var (validator, _) = await NewValidatorAsync();
        var scopes = new[]
        {
            ScopeFor("lease-execution"),                  // valid
            ScopeFor("nonexistent-A"),                    // first failure
            ScopeFor("nonexistent-B"),                    // never reached
        };

        var result = await validator.ValidateAsync(TestTenant, scopes, default);

        Assert.False(result.Passed);
        Assert.Equal("nonexistent-A", result.RejectedAt!.Code);
    }

    [Fact]
    public async Task Validate_RejectsNullScope()
    {
        var (validator, _) = await NewValidatorAsync();
        await Assert.ThrowsAsync<ArgumentNullException>(() => validator.ValidateAsync(TestTenant, null!, default));
    }

    [Fact]
    public async Task Capture_WithValidator_RejectsInvalidScope()
    {
        var registry = new InMemoryConsentRegistry();
        var consent = new ConsentRecord
        {
            Id = new ConsentRecordId(Guid.NewGuid()),
            Principal = TestSigner,
            Tenant = TestTenant,
            GivenAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            AffirmationText = "I agree.",
        };
        await registry.RecordAsync(consent, default);

        var (validator, _) = await NewValidatorAsync();
        var capture = new InMemorySignatureCapture(registry, validator, time: null);

        var request = new SignatureCaptureRequest
        {
            Tenant = TestTenant,
            Signer = TestSigner,
            Consent = consent.Id,
            DocumentHash = ContentHash.ComputeFromUtf8Nfc("doc"),
            Scope = new[] { ScopeFor("nonexistent-scope") },
            Envelope = new SignatureEnvelope("ed25519", new byte[64], new Dictionary<string, string>()),
            Quality = new CaptureQuality
            {
                StrokeFidelity = PenStrokeFidelity.LowResolution,
                ClockSource = ClockSource.ServerSide,
                DeviceTouchAvailable = true,
                DocumentReviewedBeforeSign = true,
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => capture.CaptureAsync(request, default));
    }

    [Fact]
    public void Validator_DefaultTaxonomy_IsSunfishSignatureScopes()
    {
        Assert.Equal(new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"), InMemorySignatureScopeValidator.DefaultTaxonomy);
    }

    [Fact]
    public void Validator_BoundTaxonomy_DefaultsToSignatureScopes()
    {
        var registry = new InMemoryTaxonomyRegistry();
        var resolver = new InMemoryTaxonomyResolver(registry);
        var validator = new InMemorySignatureScopeValidator(resolver);
        Assert.Equal(InMemorySignatureScopeValidator.DefaultTaxonomy, validator.BoundTaxonomy);
    }

    [Fact]
    public void Validator_BoundTaxonomy_CanBeOverridden()
    {
        var registry = new InMemoryTaxonomyRegistry();
        var resolver = new InMemoryTaxonomyResolver(registry);
        var custom = new TaxonomyDefinitionId("Acme", "Custom", "Scopes");
        var validator = new InMemorySignatureScopeValidator(resolver, custom);
        Assert.Equal(custom, validator.BoundTaxonomy);
    }
}
