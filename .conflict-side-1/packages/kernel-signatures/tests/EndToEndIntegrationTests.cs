using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Foundation.Taxonomy.Seeds;
using Sunfish.Foundation.Taxonomy.Services;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Signatures.Models;
using Sunfish.Kernel.Signatures.Services;
using Xunit;

namespace Sunfish.Kernel.Signatures.Tests;

/// <summary>
/// W#21 Phase 6 — end-to-end integration: full lifecycle through every
/// substrate piece (consent → scope-validate → capture → audit-emit →
/// revoke → projection-update → audit-emit). Mirrors the gate cited in
/// the hand-off Phase 6 spec.
/// </summary>
public sealed class EndToEndIntegrationTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly ActorId TestSigner = new("alice@example.com");
    private static readonly ActorId Operator = new("operator");

    private sealed class CapturingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();
        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
        public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var r in Records) yield return r;
            await Task.CompletedTask;
        }
    }

    private sealed class StubSigner : IOperationSigner
    {
        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(new byte[32]);
        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default) =>
            ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Signature.FromBytes(new byte[64])));
    }

    [Fact]
    public async Task FullLifecycle_CapturesAndRevokes_WithAuditTrail()
    {
        // ── Wire every piece of the substrate ───────────────────────────
        var trail = new CapturingAuditTrail();
        var emitter = new SignatureAuditEmitter(trail, new StubSigner(), TestTenant);

        var taxonomyRegistry = new InMemoryTaxonomyRegistry();
        await taxonomyRegistry.RegisterCorePackageAsync(TestTenant, TaxonomyCorePackages.SunfishSignatureScopes, default);
        var taxonomyResolver = new InMemoryTaxonomyResolver(taxonomyRegistry);

        var consents = new InMemoryConsentRegistry(emitter, time: null);
        var scopeValidator = new InMemorySignatureScopeValidator(taxonomyResolver);
        var capture = new InMemorySignatureCapture(consents, scopeValidator, emitter, time: null);
        var revocations = new InMemorySignatureRevocationLog(emitter, time: null);

        // ── 1. Record consent ───────────────────────────────────────────
        var consent = new ConsentRecord
        {
            Id = new ConsentRecordId(Guid.NewGuid()),
            Principal = TestSigner,
            Tenant = TestTenant,
            GivenAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            AffirmationText = "I agree to electronic signatures.",
        };
        await consents.RecordAsync(consent, default);

        // ── 2. Capture a signature ─────────────────────────────────────
        var captured = await capture.CaptureAsync(new SignatureCaptureRequest
        {
            Tenant = TestTenant,
            Signer = TestSigner,
            Consent = consent.Id,
            DocumentHash = ContentHash.ComputeFromUtf8Nfc("lease document body"),
            Scope = new[]
            {
                new TaxonomyClassification
                {
                    Definition = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
                    Code = "lease-execution",
                    Version = TaxonomyVersion.V1_0_0,
                },
            },
            Envelope = new SignatureEnvelope("ed25519", new byte[64], new Dictionary<string, string>()),
            Quality = new CaptureQuality
            {
                StrokeFidelity = PenStrokeFidelity.HighResolution,
                ClockSource = ClockSource.NtpVerified,
                DeviceTouchAvailable = true,
                DocumentReviewedBeforeSign = true,
            },
        }, default);

        Assert.Equal(TestSigner, captured.Signer);

        // ── 3. Verify validity (should be valid; emits SignatureValidityProjected) ──
        var verdict1 = await revocations.GetCurrentValidityAsync(captured.Id, default);
        Assert.True(verdict1.IsValid);

        // ── 4. Revoke ─────────────────────────────────────────────────
        await revocations.AppendAsync(new SignatureRevocation
        {
            Id = new RevocationEventId(Guid.NewGuid()),
            SignatureEvent = captured.Id,
            RevokedAt = DateTimeOffset.UtcNow,
            RevokedBy = Operator,
            Reason = RevocationReason.SignerRequest,
            Note = "Cooling-off withdrawal",
        }, default);

        // ── 5. Re-verify validity (should now be invalid) ─────────────
        var verdict2 = await revocations.GetCurrentValidityAsync(captured.Id, default);
        Assert.False(verdict2.IsValid);
        Assert.Equal(RevocationReason.SignerRequest, verdict2.RevokedBy!.Reason);

        // ── 6. Audit trail covers every step ──────────────────────────
        var eventTypes = trail.Records.Select(r => r.EventType).ToList();
        Assert.Contains(AuditEventType.ConsentRecorded, eventTypes);
        Assert.Contains(AuditEventType.SignatureCaptured, eventTypes);
        Assert.Contains(AuditEventType.SignatureValidityProjected, eventTypes);
        Assert.Contains(AuditEventType.SignatureRevoked, eventTypes);

        // SignatureValidityProjected should appear at least twice — once
        // before revoke, once after.
        Assert.True(eventTypes.Count(et => et == AuditEventType.SignatureValidityProjected) >= 2);
    }

    [Fact]
    public async Task CaptureRefusedWithout_ConsentOrScope()
    {
        // Wire same as above but without seeding consent or scope — capture should refuse.
        var trail = new CapturingAuditTrail();
        var emitter = new SignatureAuditEmitter(trail, new StubSigner(), TestTenant);

        var taxonomyRegistry = new InMemoryTaxonomyRegistry();
        await taxonomyRegistry.RegisterCorePackageAsync(TestTenant, TaxonomyCorePackages.SunfishSignatureScopes, default);
        var taxonomyResolver = new InMemoryTaxonomyResolver(taxonomyRegistry);

        var consents = new InMemoryConsentRegistry(emitter, time: null);
        var scopeValidator = new InMemorySignatureScopeValidator(taxonomyResolver);
        var capture = new InMemorySignatureCapture(consents, scopeValidator, emitter, time: null);

        // No consent recorded; capture should throw.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.CaptureAsync(new SignatureCaptureRequest
            {
                Tenant = TestTenant,
                Signer = TestSigner,
                Consent = new ConsentRecordId(Guid.NewGuid()),
                DocumentHash = ContentHash.ComputeFromUtf8Nfc("doc"),
                Scope = new[]
                {
                    new TaxonomyClassification
                    {
                        Definition = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
                        Code = "lease-execution",
                        Version = TaxonomyVersion.V1_0_0,
                    },
                },
                Envelope = new SignatureEnvelope("ed25519", new byte[64], new Dictionary<string, string>()),
                Quality = new CaptureQuality
                {
                    StrokeFidelity = PenStrokeFidelity.LowResolution,
                    ClockSource = ClockSource.ServerSide,
                    DeviceTouchAvailable = true,
                    DocumentReviewedBeforeSign = true,
                },
            }, default));
    }
}
