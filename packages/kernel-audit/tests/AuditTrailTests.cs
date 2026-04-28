using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Audit.DependencyInjection;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.Events.DependencyInjection;

namespace Sunfish.Kernel.Audit.Tests;

public sealed class AuditTrailTests
{
    private static (IAuditTrail trail, IAuditEventStream stream, IOperationSigner signer, KeyPair keys, ServiceProvider sp) BuildHarness()
    {
        var keys = KeyPair.Generate();
        var signer = new Ed25519Signer(keys);

        var sp = new ServiceCollection()
            .AddSunfishEventLog()
            .UseInMemoryEventLog()
            .AddSingleton<IOperationSigner>(signer)
            .AddSingleton<IOperationVerifier, Ed25519Verifier>()
            .AddSunfishKernelAudit()
            .BuildServiceProvider();

        return (
            sp.GetRequiredService<IAuditTrail>(),
            sp.GetRequiredService<IAuditEventStream>(),
            signer,
            keys,
            sp);
    }

    private static async Task<AuditRecord> SignedRecordAsync(
        IOperationSigner signer,
        TenantId tenantId,
        AuditEventType eventType,
        DateTimeOffset occurredAt)
    {
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["recoveryId"] = Guid.NewGuid().ToString("D"),
            ["note"] = "test",
        });
        var signed = await signer.SignAsync(payload, occurredAt, Guid.NewGuid());
        return new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
    }

    [Fact]
    public async Task AppendAsync_persists_record_so_query_returns_it()
    {
        var (trail, _, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-a");
            var record = await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryCompleted,
                DateTimeOffset.UtcNow);

            await trail.AppendAsync(record);

            var query = new AuditQuery(tenantId);
            var results = new List<AuditRecord>();
            await foreach (var r in trail.QueryAsync(query)) results.Add(r);

            Assert.Single(results);
            Assert.Equal(record.AuditId, results[0].AuditId);
            Assert.Equal(record.EventType, results[0].EventType);
        }
        finally
        {
            keys.Dispose();
            sp.Dispose();
        }
    }

    [Fact]
    public async Task AppendAsync_rejects_record_with_default_TenantId()
    {
        var (trail, _, signer, keys, sp) = BuildHarness();
        try
        {
            var occurredAt = DateTimeOffset.UtcNow;
            var payload = new AuditPayload(new Dictionary<string, object?> { ["x"] = 1 });
            var signed = await signer.SignAsync(payload, occurredAt, Guid.NewGuid());
            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: default, // ← invalid: IMustHaveTenant
                EventType: AuditEventType.KeyRecoveryInitiated,
                OccurredAt: occurredAt,
                Payload: signed,
                AttestingSignatures: Array.Empty<AttestingSignature>());

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await trail.AppendAsync(record));
        }
        finally
        {
            keys.Dispose();
            sp.Dispose();
        }
    }

    [Fact]
    public async Task AppendAsync_rejects_tampered_payload_signature()
    {
        var (trail, _, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-a");
            var occurredAt = DateTimeOffset.UtcNow;
            var record = await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryAttested, occurredAt);

            // Tamper the payload after signing.
            var tampered = record with
            {
                Payload = record.Payload with
                {
                    Payload = new AuditPayload(new Dictionary<string, object?>
                    {
                        ["mutated"] = true,
                    }),
                },
            };

            await Assert.ThrowsAsync<AuditSignatureException>(async () =>
                await trail.AppendAsync(tampered));
        }
        finally
        {
            keys.Dispose();
            sp.Dispose();
        }
    }

    [Fact]
    public async Task QueryAsync_filters_by_event_type_and_time_range()
    {
        var (trail, _, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-a");
            var t0 = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

            await trail.AppendAsync(await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryInitiated, t0));
            await trail.AppendAsync(await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryAttested, t0.AddMinutes(5)));
            await trail.AppendAsync(await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryCompleted, t0.AddDays(7)));

            // Filter by event type only.
            var initiated = new List<AuditRecord>();
            await foreach (var r in trail.QueryAsync(new AuditQuery(
                tenantId,
                EventType: AuditEventType.KeyRecoveryInitiated)))
            {
                initiated.Add(r);
            }
            Assert.Single(initiated);

            // Filter by time window.
            var earlyWindow = new List<AuditRecord>();
            await foreach (var r in trail.QueryAsync(new AuditQuery(
                tenantId,
                OccurredAfter: t0.AddMinutes(-1),
                OccurredBefore: t0.AddDays(1))))
            {
                earlyWindow.Add(r);
            }
            Assert.Equal(2, earlyWindow.Count);
            Assert.All(earlyWindow, r =>
                Assert.NotEqual(AuditEventType.KeyRecoveryCompleted, r.EventType));
        }
        finally
        {
            keys.Dispose();
            sp.Dispose();
        }
    }

    [Fact]
    public async Task QueryAsync_isolates_tenants()
    {
        var (trail, _, signer, keys, sp) = BuildHarness();
        try
        {
            var tA = new TenantId("tenant-a");
            var tB = new TenantId("tenant-b");
            await trail.AppendAsync(await SignedRecordAsync(
                signer, tA, AuditEventType.KeyRecoveryInitiated, DateTimeOffset.UtcNow));
            await trail.AppendAsync(await SignedRecordAsync(
                signer, tB, AuditEventType.KeyRecoveryInitiated, DateTimeOffset.UtcNow));

            var aResults = new List<AuditRecord>();
            await foreach (var r in trail.QueryAsync(new AuditQuery(tA))) aResults.Add(r);

            Assert.Single(aResults);
            Assert.Equal(tA, aResults[0].TenantId);
        }
        finally
        {
            keys.Dispose();
            sp.Dispose();
        }
    }

    [Fact]
    public async Task EventStream_publishes_to_subscribers_in_append_order()
    {
        var (trail, stream, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-a");
            var observed = new List<AuditRecord>();
            using var sub = stream.Subscribe(r => observed.Add(r));

            var t0 = DateTimeOffset.UtcNow;
            await trail.AppendAsync(await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryInitiated, t0));
            await trail.AppendAsync(await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryAttested, t0.AddMinutes(1)));

            Assert.Equal(2, observed.Count);
            Assert.Equal(AuditEventType.KeyRecoveryInitiated, observed[0].EventType);
            Assert.Equal(AuditEventType.KeyRecoveryAttested, observed[1].EventType);
        }
        finally
        {
            keys.Dispose();
            sp.Dispose();
        }
    }

    [Fact]
    public async Task AppendAsync_roundtrips_AttestingSignatures_through_QueryAsync()
    {
        var (trail, _, signer, keys, sp) = BuildHarness();
        var trustee1 = KeyPair.Generate();
        var trustee2 = KeyPair.Generate();
        try
        {
            var tenantId = new TenantId("tenant-a");
            var occurredAt = DateTimeOffset.UtcNow;
            var basePayload = new AuditPayload(new Dictionary<string, object?>
            {
                ["recoveryId"] = Guid.NewGuid().ToString("D"),
            });
            var signed = await signer.SignAsync(basePayload, occurredAt, Guid.NewGuid());

            // Two trustee attestations. v0 does not verify these algorithmically
            // — the substrate stores the (PrincipalId, Signature) pairs verbatim
            // so downstream compliance reviewers can look up keys later.
            var sig1 = await new Ed25519Signer(trustee1).SignAsync(basePayload, occurredAt, Guid.NewGuid());
            var sig2 = await new Ed25519Signer(trustee2).SignAsync(basePayload, occurredAt, Guid.NewGuid());
            var attestations = new[]
            {
                new AttestingSignature(trustee1.PrincipalId, sig1.Signature),
                new AttestingSignature(trustee2.PrincipalId, sig2.Signature),
            };

            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: tenantId,
                EventType: AuditEventType.KeyRecoveryCompleted,
                OccurredAt: occurredAt,
                Payload: signed,
                AttestingSignatures: attestations);

            await trail.AppendAsync(record);

            var results = new List<AuditRecord>();
            await foreach (var r in trail.QueryAsync(new AuditQuery(tenantId)))
            {
                results.Add(r);
            }

            Assert.Single(results);
            var roundtripped = results[0].AttestingSignatures;
            Assert.Equal(2, roundtripped.Count);
            Assert.Equal(trustee1.PrincipalId, roundtripped[0].PrincipalId);
            Assert.Equal(sig1.Signature, roundtripped[0].Signature);
            Assert.Equal(trustee2.PrincipalId, roundtripped[1].PrincipalId);
            Assert.Equal(sig2.Signature, roundtripped[1].Signature);
        }
        finally
        {
            trustee2.Dispose();
            trustee1.Dispose();
            keys.Dispose();
            sp.Dispose();
        }
    }

    [Fact]
    public async Task AppendAsync_persists_to_kernel_event_log()
    {
        var (trail, _, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-a");
            var record = await SignedRecordAsync(
                signer, tenantId, AuditEventType.KeyRecoveryCompleted,
                DateTimeOffset.UtcNow);
            await trail.AppendAsync(record);

            var log = sp.GetRequiredService<IEventLog>();
            var entries = new List<LogEntry>();
            await foreach (var entry in log.ReadAfterAsync(0, default))
            {
                entries.Add(entry);
            }

            Assert.Single(entries);
            Assert.StartsWith("audit.", entries[0].Event.Kind);
            Assert.Equal("audit." + AuditEventType.KeyRecoveryCompleted.Value,
                entries[0].Event.Kind);
        }
        finally
        {
            keys.Dispose();
            sp.Dispose();
        }
    }
}
