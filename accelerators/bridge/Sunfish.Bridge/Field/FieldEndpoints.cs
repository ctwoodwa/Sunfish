using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Bridge.Field;

/// <summary>
/// Bridge route family for the W#23 iOS Field-Capture App per the W#23
/// P4 + P4.5 unblock addenda. Hosts <c>POST /api/v1/field/event</c> +
/// <c>POST /api/v1/field/blob/{sha256}</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>v1 substrate scope</b> (per the W#23 P4 + P4.5 unblock addenda):
/// route handlers + audit emission via the in-memory <see cref="IAuditTrail"/>
/// + idempotency on <c>eventId</c> + SHA-256 path-param verification on the
/// blob endpoint + content-addressed local-disk blob storage. Pairing-token
/// JWT validation per ADR 0028 §A2.6 + W#28 P5b defense-pipeline composition
/// land as follow-up work — substrate v1 accepts requests on the route
/// surface so the iOS sync engine can smoke-test end-to-end against a
/// development Bridge while those auth + defense layers ship in parallel.
/// </para>
/// <para>
/// <b>Audit emission is W#32 both-or-neither</b>: failed audit emission
/// rejects the request with HTTP 500 + an <c>audit-emission-failed</c>
/// error code. Per the audit-infra unblock addendum, the
/// <see cref="InMemoryAuditTrail"/> registration is restart-volatile;
/// persistent infra defers to ~ADR 0076.
/// </para>
/// </remarks>
public static class FieldEndpoints
{
    private const long MaxBlobBytes = 10L * 1024 * 1024;

    /// <summary>
    /// Sentinel tenant attribution for unauthenticated / pre-validation
    /// audit records. Substrate v1 uses an explicit sentinel because
    /// <see cref="TenantId.System"/> is reserved for trusted system-actor
    /// context per ADR 0084 §1 and would mis-attribute pre-validation
    /// records to the system principal.
    /// </summary>
    private static readonly TenantId BridgeAnonymousTenant = new("bridge-anonymous");

    /// <summary>
    /// Configurable blob-storage root. Reads <c>Bridge:Field:BlobRoot</c>
    /// from configuration; falls back to <c>var/blobs/</c> next to the
    /// current working directory. Production deploys MUST set the option
    /// when <see cref="AppContext.BaseDirectory"/> is read-only
    /// (containerized Bridge).
    /// </summary>
    private static string ResolveBlobRoot(HttpRequest request)
    {
        var configured = request.HttpContext.RequestServices
            .GetService<Microsoft.Extensions.Configuration.IConfiguration>()?
            .GetValue<string>("Bridge:Field:BlobRoot");
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "var", "blobs");
    }

    /// <summary>
    /// In-process idempotency cache: keys on <c>eventId</c>; value is the
    /// canonical-JSON envelope bytes that produced the original 200 response.
    /// Restart-volatile (matches the audit-trail's v1 in-memory posture).
    /// Phase 4 substrate; Phase 4+ replaces with persistent storage.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, ImmutableArray<byte>> _eventIdempotencyCache = new();

    /// <summary>Wires the W#23 field route family onto the Bridge.</summary>
    public static IEndpointRouteBuilder MapFieldEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1/field");
        group.MapPost("/event", HandleFieldEventPostAsync);
        group.MapPost("/blob/{sha256}", HandleFieldBlobPostAsync);
        group.MapPost("/unpair", HandleFieldUnpairAsync);
        return app;
    }

    /// <summary>
    /// Accept a field-event envelope from a paired iOS device. Per ADR
    /// 0028-A1 (envelope shape) + post-A9 wire shape (PR #516).
    /// </summary>
    internal static async Task<IResult> HandleFieldEventPostAsync(
        HttpRequest request,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);

        // Read the envelope body.
        using var bodyReader = new StreamReader(request.Body, Encoding.UTF8);
        var bodyText = await bodyReader.ReadToEndAsync(ct).ConfigureAwait(false);

        FieldEventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<FieldEventEnvelope>(
                bodyText,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch (JsonException ex)
        {
            await EmitAuditAsync(auditTrail, signer,
                AuditEventType.FieldEventRejected,
                tenantId: BridgeAnonymousTenant,
                payload: BuildRejectPayload("schema-validation-failed", ex.Message),
                ct).ConfigureAwait(false);
            return Results.BadRequest(new { error = "schema-validation-failed", detail = ex.Message });
        }
        if (envelope is null || envelope.EventId == Guid.Empty)
        {
            await EmitAuditAsync(auditTrail, signer,
                AuditEventType.FieldEventRejected,
                tenantId: BridgeAnonymousTenant,
                payload: BuildRejectPayload("missing-event-id", "envelope eventId is required"),
                ct).ConfigureAwait(false);
            return Results.BadRequest(new { error = "missing-event-id" });
        }

        // Idempotency: re-POST of the same eventId returns the original
        // success response. Uses Sunfish.Foundation.Crypto.CanonicalJson
        // for byte-stable comparison so two semantically-equal envelopes
        // whose payload JsonElement differs in key order do not trip a
        // spurious 409.
        var canonicalBytes = CanonicalJson.Serialize(envelope);
        if (_eventIdempotencyCache.TryGetValue(envelope.EventId, out var stored))
        {
            // Diverging content under the same eventId is a signature drift —
            // 409 Conflict per the unblock addendum.
            if (!System.Linq.Enumerable.SequenceEqual(stored, canonicalBytes))
            {
                await EmitAuditAsync(auditTrail, signer,
                    AuditEventType.FieldEventRejected,
                    envelope.TenantId,
                    BuildRejectPayload("eventid-conflict", "eventId reused with different content"),
                    ct).ConfigureAwait(false);
                return Results.Conflict(new { error = "eventid-conflict" });
            }
            return Results.Ok(new { eventId = envelope.EventId, accepted_at = DateTimeOffset.UtcNow });
        }

        _eventIdempotencyCache[envelope.EventId] = ImmutableArray.Create(canonicalBytes);

        await EmitAuditAsync(auditTrail, signer,
            AuditEventType.FieldEventAccepted,
            envelope.TenantId,
            BuildAcceptPayload(envelope.EventId, envelope.EventType, envelope.DeviceId),
            ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            eventId = envelope.EventId,
            accepted_at = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>
    /// Accept a content-addressed binary blob from a paired iOS device.
    /// Path param <c>sha256</c> is the lowercase hex SHA-256 of the request
    /// body; server verifies the hash matches.
    /// </summary>
    internal static async Task<IResult> HandleFieldBlobPostAsync(
        HttpRequest request,
        string sha256,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);

        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64)
        {
            await EmitAuditAsync(auditTrail, signer,
                AuditEventType.FieldBlobRejected,
                tenantId: BridgeAnonymousTenant,
                payload: BuildRejectPayload("invalid-sha256-format", "path param must be 64 hex chars"),
                ct).ConfigureAwait(false);
            return Results.BadRequest(new { error = "invalid-sha256-format" });
        }

        if (request.ContentLength > MaxBlobBytes)
        {
            await EmitAuditAsync(auditTrail, signer,
                AuditEventType.FieldBlobRejected,
                tenantId: BridgeAnonymousTenant,
                payload: BuildRejectPayload("payload-too-large", $"max {MaxBlobBytes} bytes"),
                ct).ConfigureAwait(false);
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        await using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
        if (ms.Length > MaxBlobBytes)
        {
            await EmitAuditAsync(auditTrail, signer,
                AuditEventType.FieldBlobRejected,
                tenantId: BridgeAnonymousTenant,
                payload: BuildRejectPayload("payload-too-large", $"max {MaxBlobBytes} bytes"),
                ct).ConfigureAwait(false);
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var bytes = ms.ToArray();
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actualHash, sha256, StringComparison.Ordinal))
        {
            await EmitAuditAsync(auditTrail, signer,
                AuditEventType.FieldBlobRejected,
                tenantId: BridgeAnonymousTenant,
                payload: BuildRejectPayload("sha256-mismatch", $"expected {sha256}; got {actualHash}"),
                ct).ConfigureAwait(false);
            return Results.BadRequest(new { error = "sha256-mismatch" });
        }

        // Local-disk content-addressed storage per the unblock addendum
        // halt-condition #4 (default backend): var/blobs/<sha256[0:2]>/<sha256>.
        // Path is configurable via Bridge:Field:BlobRoot — production
        // deploys MUST set the option if the current-working-directory
        // default is not writable.
        var blobRoot = Path.Combine(ResolveBlobRoot(request), sha256[..2]);
        Directory.CreateDirectory(blobRoot);
        var blobPath = Path.Combine(blobRoot, sha256);
        if (!File.Exists(blobPath))
        {
            await File.WriteAllBytesAsync(blobPath, bytes, ct).ConfigureAwait(false);
        }

        await EmitAuditAsync(auditTrail, signer,
            AuditEventType.FieldBlobAccepted,
            // Council C2 (W#1 WS-A): match the FieldBlobRejected paths
            // (BridgeAnonymousTenant). The endpoint is unauthenticated;
            // attributing accepted-blob audits to TenantId.System would
            // mis-attribute pre-validation traffic to the system actor.
            tenantId: BridgeAnonymousTenant,
            payload: BuildBlobAcceptPayload(sha256, bytes.LongLength, request.ContentType),
            ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            sha256,
            blob_url = $"/api/v1/field/blob/{sha256}",
        });
    }

    // Per ADR 0028-A2.8: device_id is a hex string of fixed length (16 chars).
    // Council Security-B1 (2026-05-13): header is attacker-supplied on this
    // unauthenticated substrate-v1 endpoint; validate before writing to the
    // signed audit log to prevent log injection or audit poisoning.
    private static readonly System.Text.RegularExpressions.Regex SafeDeviceIdPattern =
        new(@"^[0-9a-f]{1,64}$",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Revoke a paired device. The iOS app calls this when the user taps
    /// "Unpair this device"; Bridge emits <see cref="AuditEventType.FieldDeviceRevoked"/>
    /// and returns 204. Per W#23 Phase 6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Substrate v1 scope:</b> audit emission + 204 response. Full token
    /// invalidation (blacklisting the pairing-token in a persistent store)
    /// ships as part of the auth-layer follow-up (ADR 0028-A2.6). This
    /// endpoint is LAN-only in substrate-v1 deployments; WAN exposure
    /// requires the auth layer.
    /// </para>
    /// <para>
    /// <b>Security posture (Council Security-B1 + B2 amendment, 2026-05-13):</b>
    /// <c>X-Sunfish-Device-Id</c> is validated against <c>^[0-9a-f]{1,64}$</c>
    /// before writing to the signed audit log. Rejected or absent headers
    /// are logged as <c>device_id: null</c> with <c>source: "client-claimed"</c>
    /// so forensics can distinguish asserted-vs-verified identity. Rate
    /// limiting is deferred to the auth-layer follow-up; document in the
    /// Phase 7 hand-off that this route must be protected before WAN exposure.
    /// </para>
    /// </remarks>
    internal static async Task<IResult> HandleFieldUnpairAsync(
        HttpRequest request,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);

        // Read and validate the device-id header. Council Security-B1:
        // do not write raw caller-supplied strings to the signed audit log.
        string? deviceId = null;
        if (request.Headers.TryGetValue("X-Sunfish-Device-Id", out var hv))
        {
            var candidate = hv.ToString();
            if (SafeDeviceIdPattern.IsMatch(candidate))
                deviceId = candidate;
            // Invalid header value: leave deviceId null; log as unrecognised below.
        }

        await EmitAuditAsync(
            auditTrail, signer,
            AuditEventType.FieldDeviceRevoked,
            tenantId: BridgeAnonymousTenant,
            payload: new AuditPayload(new Dictionary<string, object?>
            {
                ["device_id"] = deviceId,
                ["source"] = "client-claimed",   // distinguishes from future auth-verified device_id
            }),
            ct).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static AuditPayload BuildAcceptPayload(Guid eventId, string? eventType, string? deviceId)
        => new(new Dictionary<string, object?>
        {
            ["device_id"] = deviceId,
            ["event_id"] = eventId.ToString("D"),
            ["event_type"] = eventType,
        });

    private static AuditPayload BuildRejectPayload(string reason, string detail)
        => new(new Dictionary<string, object?>
        {
            ["detail"] = detail,
            ["reason"] = reason,
        });

    private static AuditPayload BuildBlobAcceptPayload(string sha256, long byteCount, string? mimeType)
        => new(new Dictionary<string, object?>
        {
            ["byte_count"] = byteCount,
            ["mime_type"] = mimeType,
            ["sha256"] = sha256,
        });

    private static async Task EmitAuditAsync(
        IAuditTrail auditTrail,
        IOperationSigner signer,
        AuditEventType eventType,
        TenantId tenantId,
        AuditPayload payload,
        CancellationToken ct)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid();
        var signed = await signer.SignAsync(payload, occurredAt, nonce, ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId == default ? new TenantId("bridge-anonymous") : tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    /// <summary>Wire-format envelope as parsed off the request body.</summary>
    /// <remarks>
    /// Field shape mirrors <c>EventEnvelope</c> on the iOS side (W#23 P3
    /// PR #516 post-A9 wire shape). Bridge accepts the envelope as opaque
    /// JSON for substrate v1; signature verification + per-event-type
    /// schema validation are follow-up work.
    /// </remarks>
    internal sealed record FieldEventEnvelope(
        Guid EventId,
        TenantId TenantId,
        string ActorId,
        string EventType,
        JsonElement Payload,
        DateTimeOffset CapturedAt,
        string CapturedUnderKernel,
        uint CapturedUnderSchemaEpoch,
        string DeviceId);
}
