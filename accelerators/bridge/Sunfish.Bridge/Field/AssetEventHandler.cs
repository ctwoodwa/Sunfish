using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Bridge.Field;

/// <summary>
/// Handles <c>EventType == "Asset"</c> field-event envelopes. Resolves the
/// equipment referenced in the payload and stamps
/// <see cref="Equipment.PrimaryPhotoBlobRef"/> with the envelope's blob address.
/// </summary>
internal static class AssetEventHandler
{
    internal static async Task<IResult> HandleAsync(
        FieldEventEnvelope envelope,
        IEquipmentRepository equipmentRepository,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(equipmentRepository);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);

        AssetCapturePayload? capturePayload;
        try
        {
            capturePayload = JsonSerializer.Deserialize<AssetCapturePayload>(
                envelope.Payload.GetRawText());
        }
        catch (JsonException ex)
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-payload-schema-failed", ex.Message, ct).ConfigureAwait(false);
            return Results.BadRequest(new { error = "asset-payload-schema-failed", detail = ex.Message });
        }

        if (capturePayload is null || string.IsNullOrWhiteSpace(capturePayload.EquipmentId))
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-missing-equipment-id", "equipmentId is required in Asset payload", ct)
                .ConfigureAwait(false);
            return Results.BadRequest(new { error = "asset-missing-equipment-id" });
        }

        // M2 (council): guard equipmentId format before it reaches audit logs or the
        // future EFCore-backed repository; mirrors the audit-payload injection defence.
        if (capturePayload.EquipmentId.Length > 128 ||
            !IsSafeIdChars(capturePayload.EquipmentId))
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-invalid-equipment-id-format",
                "equipmentId must be ≤128 chars of letters, digits, hyphens, or underscores",
                ct).ConfigureAwait(false);
            return Results.BadRequest(new { error = "asset-invalid-equipment-id-format" });
        }

        if (string.IsNullOrWhiteSpace(envelope.BlobRef))
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-missing-blob-ref", "blobRef is required for EventType.Asset", ct)
                .ConfigureAwait(false);
            return Results.UnprocessableEntity(new { error = "asset-missing-blob-ref" });
        }

        // H1 (council): blobRef must be a 64-char lowercase hex SHA-256 — mirrors the
        // blob-upload endpoint invariant (FieldEndpoints.HandleFieldBlobPostAsync).
        // Prevents arbitrary strings from being written to Equipment.PrimaryPhotoBlobRef.
        if (envelope.BlobRef.Length != 64 || !IsLowercaseHex64(envelope.BlobRef))
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-invalid-blob-ref",
                "blobRef must be 64 lowercase hex chars (SHA-256)", ct)
                .ConfigureAwait(false);
            return Results.BadRequest(new { error = "asset-invalid-blob-ref" });
        }

        var equipmentId = new EquipmentId(capturePayload.EquipmentId);
        var existing = await equipmentRepository
            .GetByIdAsync(envelope.TenantId, equipmentId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-equipment-not-found",
                $"equipmentId {capturePayload.EquipmentId} not found",
                ct).ConfigureAwait(false);
            return Results.NotFound(new { error = "asset-equipment-not-found" });
        }

        var updated = existing with { PrimaryPhotoBlobRef = envelope.BlobRef };
        await equipmentRepository.UpsertAsync(updated, ct).ConfigureAwait(false);

        await EmitAcceptAsync(auditTrail, signer, envelope.TenantId,
            envelope.EventId, capturePayload.EquipmentId, envelope.BlobRef, ct)
            .ConfigureAwait(false);

        return Results.Ok(new
        {
            eventId = envelope.EventId,
            equipmentId = capturePayload.EquipmentId,
            primaryPhotoBlobRef = envelope.BlobRef,
        });
    }

    private static bool IsLowercaseHex64(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        return true;
    }

    private static bool IsSafeIdChars(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
        }
        return true;
    }

    private static Task EmitAcceptAsync(
        IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId,
        Guid eventId, string equipmentId, string blobRef, CancellationToken ct)
    {
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["blob_ref"]     = blobRef,
            ["equipment_id"] = equipmentId,
            ["event_id"]     = eventId.ToString("D"),
        });
        return FieldEndpoints.EmitAuditAsync(
            auditTrail, signer, AuditEventType.FieldAssetPhotoAccepted, tenantId, payload, ct);
    }

    private static Task EmitRejectAsync(
        IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId,
        string reason, string detail, CancellationToken ct)
    {
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["detail"] = detail,
            ["reason"] = reason,
        });
        return FieldEndpoints.EmitAuditAsync(
            auditTrail, signer, AuditEventType.FieldAssetPhotoRejected, tenantId, payload, ct);
    }
}
