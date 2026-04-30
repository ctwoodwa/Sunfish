using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Signatures.Models;
using Sunfish.Kernel.Signatures.Services;
using Xunit;

namespace Sunfish.Kernel.Signatures.Tests;

/// <summary>
/// W#21 Phase 5 — verifies the 5 AuditEventType constants emit cleanly
/// from the 3 InMemory services + that payload bodies carry the
/// expected keys.
/// </summary>
public sealed class AuditEmissionTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly ActorId TestSigner = new("alice@example.com");

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

    private static (SignatureAuditEmitter emitter, CapturingAuditTrail trail) NewEmitter()
    {
        var trail = new CapturingAuditTrail();
        var emitter = new SignatureAuditEmitter(trail, new StubSigner(), TestTenant);
        return (emitter, trail);
    }

    [Fact]
    public async Task ConsentRecorded_Emits()
    {
        var (emitter, trail) = NewEmitter();
        var registry = new InMemoryConsentRegistry(emitter, time: null);

        await registry.RecordAsync(MakeConsent(), default);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.ConsentRecorded, trail.Records[0].EventType);
    }

    [Fact]
    public async Task ConsentRevoked_Emits()
    {
        var (emitter, trail) = NewEmitter();
        var registry = new InMemoryConsentRegistry(emitter, time: null);
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);

        await registry.RevokeAsync(consent.Id, DateTimeOffset.UtcNow, default);

        Assert.Equal(2, trail.Records.Count);
        Assert.Equal(AuditEventType.ConsentRevoked, trail.Records[1].EventType);
    }

    [Fact]
    public async Task ConsentRevoked_NoEmission_WhenIdUnknown()
    {
        var (emitter, trail) = NewEmitter();
        var registry = new InMemoryConsentRegistry(emitter, time: null);
        await registry.RevokeAsync(new ConsentRecordId(Guid.NewGuid()), DateTimeOffset.UtcNow, default);
        Assert.Empty(trail.Records);
    }

    [Fact]
    public async Task SignatureCaptured_Emits()
    {
        var (emitter, trail) = NewEmitter();
        var registry = new InMemoryConsentRegistry(emitter, time: null);
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);
        var capture = new InMemorySignatureCapture(registry, scopeValidator: null, audit: emitter, time: null);

        await capture.CaptureAsync(MakeRequest(consent.Id), default);

        // 1 ConsentRecorded + 1 SignatureCaptured.
        Assert.Equal(2, trail.Records.Count);
        Assert.Equal(AuditEventType.SignatureCaptured, trail.Records[1].EventType);
    }

    [Fact]
    public async Task SignatureCaptured_BodyCarriesEnvelopeAndQuality()
    {
        var (emitter, trail) = NewEmitter();
        var registry = new InMemoryConsentRegistry(emitter, time: null);
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);
        var capture = new InMemorySignatureCapture(registry, scopeValidator: null, audit: emitter, time: null);

        await capture.CaptureAsync(MakeRequest(consent.Id), default);

        var body = trail.Records.Last().Payload.Payload.Body;
        Assert.Equal("ed25519", body["envelope_algorithm"]);
        Assert.Equal("ServerSide", body["clock_source"]);
        Assert.Equal("LowResolution", body["stroke_fidelity"]);
    }

    [Fact]
    public async Task SignatureRevoked_Emits()
    {
        var (emitter, trail) = NewEmitter();
        var log = new InMemorySignatureRevocationLog(emitter, time: null);

        var revocation = new SignatureRevocation
        {
            Id = new RevocationEventId(Guid.NewGuid()),
            SignatureEvent = new SignatureEventId(Guid.NewGuid()),
            RevokedAt = DateTimeOffset.UtcNow,
            RevokedBy = new ActorId("operator"),
            Reason = RevocationReason.SignerRequest,
        };
        await log.AppendAsync(revocation, default);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.SignatureRevoked, trail.Records[0].EventType);
    }

    [Fact]
    public async Task SignatureRevoked_DoesNotEmit_OnDuplicateAppend()
    {
        var (emitter, trail) = NewEmitter();
        var log = new InMemorySignatureRevocationLog(emitter, time: null);
        var revocation = new SignatureRevocation
        {
            Id = new RevocationEventId(Guid.NewGuid()),
            SignatureEvent = new SignatureEventId(Guid.NewGuid()),
            RevokedAt = DateTimeOffset.UtcNow,
            RevokedBy = new ActorId("operator"),
            Reason = RevocationReason.SignerRequest,
        };
        await log.AppendAsync(revocation, default);
        await log.AppendAsync(revocation, default); // duplicate — idempotent dedup

        Assert.Single(trail.Records);
    }

    [Fact]
    public async Task SignatureValidityProjected_Emits_OnEachQuery()
    {
        var (emitter, trail) = NewEmitter();
        var log = new InMemorySignatureRevocationLog(emitter, time: null);
        var sigId = new SignatureEventId(Guid.NewGuid());

        await log.GetCurrentValidityAsync(sigId, default);
        await log.GetCurrentValidityAsync(sigId, default);

        Assert.Equal(2, trail.Records.Count);
        Assert.All(trail.Records, r => Assert.Equal(AuditEventType.SignatureValidityProjected, r.EventType));
    }

    [Fact]
    public async Task SignatureValidityProjected_Body_CarriesValidityFlag()
    {
        var (emitter, trail) = NewEmitter();
        var log = new InMemorySignatureRevocationLog(emitter, time: null);
        var sigId = new SignatureEventId(Guid.NewGuid());

        // Fresh — should project as valid.
        await log.GetCurrentValidityAsync(sigId, default);
        Assert.Equal(true, trail.Records[0].Payload.Payload.Body["is_valid"]);

        // Append a revocation, re-query — should project as invalid.
        await log.AppendAsync(new SignatureRevocation
        {
            Id = new RevocationEventId(Guid.NewGuid()),
            SignatureEvent = sigId,
            RevokedAt = DateTimeOffset.UtcNow,
            RevokedBy = new ActorId("operator"),
            Reason = RevocationReason.SignerRequest,
        }, default);
        await log.GetCurrentValidityAsync(sigId, default);

        Assert.Equal(false, trail.Records.Last().Payload.Payload.Body["is_valid"]);
    }

    [Fact]
    public async Task NoEmission_WhenAuditUnwired()
    {
        var registry = new InMemoryConsentRegistry();
        var capture = new InMemorySignatureCapture(registry);
        var log = new InMemorySignatureRevocationLog();

        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);
        await capture.CaptureAsync(MakeRequest(consent.Id), default);
        await log.GetCurrentValidityAsync(new SignatureEventId(Guid.NewGuid()), default);
        // No assertion possible — but the lack of any audit dependency
        // proves the wiring is opt-in. This test validates there are
        // no NREs / null-emitter exceptions when audit is disabled.
    }

    [Fact]
    public void Emitter_RejectsNullAndDefaultArgs()
    {
        var trail = new CapturingAuditTrail();
        Assert.Throws<ArgumentNullException>(() => new SignatureAuditEmitter(null!, new StubSigner(), TestTenant));
        Assert.Throws<ArgumentNullException>(() => new SignatureAuditEmitter(trail, null!, TestTenant));
        Assert.Throws<ArgumentException>(() => new SignatureAuditEmitter(trail, new StubSigner(), default));
    }

    [Fact]
    public async Task TenantAttribution_AppliedToAllRecords()
    {
        var (emitter, trail) = NewEmitter();
        var registry = new InMemoryConsentRegistry(emitter, time: null);
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);
        await registry.RevokeAsync(consent.Id, DateTimeOffset.UtcNow, default);

        Assert.All(trail.Records, r => Assert.Equal(TestTenant, r.TenantId));
    }

    // ─────────── Helpers ───────────

    private static ConsentRecord MakeConsent() => new()
    {
        Id = new ConsentRecordId(Guid.NewGuid()),
        Principal = TestSigner,
        Tenant = TestTenant,
        GivenAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        AffirmationText = "I agree.",
    };

    private static SignatureCaptureRequest MakeRequest(ConsentRecordId consentId) => new()
    {
        Tenant = TestTenant,
        Signer = TestSigner,
        Consent = consentId,
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
    };
}
