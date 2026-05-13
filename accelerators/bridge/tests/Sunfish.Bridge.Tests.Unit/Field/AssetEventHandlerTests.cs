using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Bridge.Field;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Field;

public sealed class AssetEventHandlerTests
{
    private static readonly TenantId TestTenant = new("test-tenant");
    private static readonly PropertyId TestProperty = new("prop-001");

    [Fact]
    public async Task HandleAsync_AcceptsValidPayload_UpdatesEquipmentAndReturnsOk()
    {
        var (audit, signer) = MakeServices();
        var repo = new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore());
        var equipId = new EquipmentId("equip-abc");
        await repo.UpsertAsync(MakeEquipment(equipId), CancellationToken.None);
        const string blobRef = "aabbccdd11223344556677889900112233445566778899001122334455667788"; // 64 hex chars

        var envelope = MakeEnvelope(
            eventType: "Asset",
            blobRef: blobRef,
            payload: JsonSerializer.SerializeToElement(new { equipmentId = "equip-abc", photoKind = "primary" }));

        var result = await AssetEventHandler.HandleAsync(envelope, repo, audit, signer, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)result).StatusCode);

        var updated = await repo.GetByIdAsync(TestTenant, equipId, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(blobRef, updated!.PrimaryPhotoBlobRef);

        var records = await CollectAuditAsync(audit);
        Assert.Single(records, r => r.EventType == AuditEventType.FieldAssetPhotoAccepted);
    }

    [Fact]
    public async Task HandleAsync_EquipmentNotFound_Returns404AndEmitsRejectedAudit()
    {
        var (audit, signer) = MakeServices();
        var repo = new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore());

        var envelope = MakeEnvelope(
            eventType: "Asset",
            blobRef: "aabb" + new string('0', 58) + "ff",  // 64 valid hex chars
            payload: JsonSerializer.SerializeToElement(new { equipmentId = "nonexistent", photoKind = "primary" }));

        var result = await AssetEventHandler.HandleAsync(envelope, repo, audit, signer, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
        var records = await CollectAuditAsync(audit);
        Assert.Single(records, r => r.EventType == AuditEventType.FieldAssetPhotoRejected);
    }

    [Fact]
    public async Task HandleAsync_MissingBlobRef_Returns422()
    {
        var (audit, signer) = MakeServices();
        var repo = new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore());

        var envelope = MakeEnvelope(
            eventType: "Asset",
            blobRef: null,
            payload: JsonSerializer.SerializeToElement(new { equipmentId = "equip-abc", photoKind = "primary" }));

        var result = await AssetEventHandler.HandleAsync(envelope, repo, audit, signer, CancellationToken.None);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task HandleAsync_MalformedBlobRef_Returns400()
    {
        // H1 council amendment: blobRef must be 64 lowercase hex chars.
        var (audit, signer) = MakeServices();
        var repo = new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore());

        var envelope = MakeEnvelope(
            eventType: "Asset",
            blobRef: "not-a-valid-sha256",
            payload: JsonSerializer.SerializeToElement(new { equipmentId = "equip-abc", photoKind = "primary" }));

        var result = await AssetEventHandler.HandleAsync(envelope, repo, audit, signer, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)result).StatusCode);
    }

    // ===== Helpers =====

    private static (InMemoryAuditTrail Audit, IOperationSigner Signer) MakeServices()
        => (new InMemoryAuditTrail(), new Ed25519Signer(KeyPair.Generate()));

    private static FieldEventEnvelope MakeEnvelope(
        string eventType,
        string? blobRef,
        JsonElement payload)
        => new(
            EventId: Guid.NewGuid(),
            TenantId: TestTenant,
            ActorId: "actor-test",
            EventType: eventType,
            Payload: payload,
            CapturedAt: DateTimeOffset.UtcNow,
            CapturedUnderKernel: "1.0.0",
            CapturedUnderSchemaEpoch: 1u,
            DeviceId: "00112233445566778899aabbccddeeff",
            BlobRef: blobRef);

    private static Equipment MakeEquipment(EquipmentId id)
        => new()
        {
            Id = id,
            TenantId = TestTenant,
            Property = TestProperty,
            Class = EquipmentClass.WaterHeater,
            DisplayName = "Test Unit",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static async Task<List<AuditRecord>> CollectAuditAsync(InMemoryAuditTrail audit)
    {
        var list = new List<AuditRecord>();
        await foreach (var r in audit.QueryAsync(new AuditQuery(TenantId: TestTenant)))
            list.Add(r);
        return list;
    }
}
