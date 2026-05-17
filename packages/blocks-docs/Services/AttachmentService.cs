using System.Security.Cryptography;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Default <see cref="IAttachmentService"/>. Performs sha-256 content
/// hashing + dedup; runs every upload through the PR 3 defense-in-depth
/// gates (server-side MIME sniffer, filename sanitizer, MIME + size +
/// tenant-quota policy) before persisting bytes.
///
/// <para>
/// <b>Upload order (council review):</b>
/// </para>
/// <list type="number">
/// <item>Sniff the real MIME from leading magic bytes (filename ext is not trusted).</item>
/// <item>Sanitize the original filename (strip path components, reject control chars / reserved names).</item>
/// <item>Run the three-gate policy (MIME whitelist + per-attachment cap + tenant quota).</item>
/// <item>Compute sha-256 + dedup against existing tenant attachments.</item>
/// <item>Persist via <see cref="IAttachmentRepository"/>.</item>
/// </list>
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachments;
    private readonly IMimeTypeAndSizePolicy? _policy;

    /// <summary>
    /// Construct with optional policy. When <paramref name="policy"/> is
    /// null (test fixtures, sketches) the service falls through to the
    /// PR 2 behavior — no MIME / size / quota gating. Production
    /// registrations supply a real
    /// <see cref="MimeTypeAndSizePolicy"/>.
    /// </summary>
    public AttachmentService(
        IAttachmentRepository attachments,
        IMimeTypeAndSizePolicy? policy = null)
    {
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _policy = policy;
    }

    /// <inheritdoc />
    public async Task<Attachment> UploadAsync(
        TenantId tenantId,
        ReadOnlyMemory<byte> bytes,
        string mimeType,
        string originalFilename,
        string createdBy,
        Sensitivity sensitivity = Sensitivity.Internal,
        CancellationToken cancellationToken = default)
    {
        // 1. Server-side MIME sniff. We deliberately IGNORE the caller-
        // supplied mimeType for the policy decision — the filename ext
        // and the Content-Type header are not trusted.
        var sniffedMime = MimeSniffer.Sniff(bytes.Span);

        // 2. Sanitize the filename. Fall back to a hash-derived safe name
        // if the input doesn't sanitize.
        var safeFilename = FilenameSanitizer.Sanitize(originalFilename) ?? "attachment.bin";

        // 3. Three-gate policy check (only when the host wired one).
        if (_policy is not null)
        {
            var result = await _policy.ValidateAsync(tenantId, sniffedMime, bytes.Length, cancellationToken)
                .ConfigureAwait(false);
            if (result.Rejected)
                throw new UploadRejectedException(result.RejectionReason, result.Detail ?? "Upload rejected by policy.");
        }

        var hash = ComputeSha256Hex(bytes.Span);

        // 4. Dedup: same (tenant, hash) → reuse the existing Active attachment.
        var existing = await _attachments.FindByContentHashAsync(tenantId, hash, cancellationToken)
            .ConfigureAwait(false);
        var existingActive = existing.FirstOrDefault(a => a.Status == AttachmentStatus.Active);
        if (existingActive is not null) return existingActive;

        // 5. Persist. PR 3 keeps the Inline storage tier; FoundationBlob
        // wiring lands in a follow-on when IBlobStore DI is fully threaded.
        var attachment = Attachment.Create(
            tenantId: tenantId,
            storageRef: StorageRef.ForInline(bytes),
            contentHash: hash,
            mimeType: sniffedMime,         // Persist the sniffed MIME, not the caller-supplied one.
            sizeBytes: bytes.Length,
            originalFilename: safeFilename, // Persist the sanitized name.
            createdBy: createdBy,
            sensitivity: sensitivity);

        await _attachments.UpsertAsync(attachment, cancellationToken).ConfigureAwait(false);
        return attachment;
    }

    /// <inheritdoc />
    public async Task<Attachment> SupersedeAsync(
        AttachmentId priorAttachmentId,
        ReadOnlyMemory<byte> bytes,
        string mimeType,
        string originalFilename,
        string updatedBy,
        Sensitivity? sensitivity = null,
        CancellationToken cancellationToken = default)
    {
        var prior = await _attachments.GetAsync(priorAttachmentId, cancellationToken).ConfigureAwait(false);
        if (prior is null)
            throw new InvalidOperationException($"Attachment '{priorAttachmentId.Value}' does not exist or is tombstoned.");
        if (prior.Status != AttachmentStatus.Active)
            throw new InvalidOperationException($"Attachment '{priorAttachmentId.Value}' is in status '{prior.Status}'; only Active attachments can be superseded.");

        var hash = ComputeSha256Hex(bytes.Span);

        // Dedup: if the new bytes already exist as an Active attachment in
        // the tenant, use that row as the new version (still flip the prior
        // to Superseded against it).
        var existingByHash = await _attachments.FindByContentHashAsync(prior.TenantId, hash, cancellationToken)
            .ConfigureAwait(false);
        var dedupTarget = existingByHash.FirstOrDefault(a =>
            a.Status == AttachmentStatus.Active && a.Id != prior.Id);

        Attachment newAttachment;
        if (dedupTarget is not null)
        {
            // The new version already exists as a separate Active row —
            // back-fill its ReplacesAttachmentId only if it's blank; never
            // rewrite an existing replacement chain.
            newAttachment = dedupTarget.ReplacesAttachmentId is null
                ? dedupTarget with
                {
                    ReplacesAttachmentId = prior.Id,
                    UpdatedAtUtc = Instant.Now,
                    UpdatedBy = updatedBy,
                    Version = dedupTarget.Version + 1,
                }
                : dedupTarget;

            if (!ReferenceEquals(newAttachment, dedupTarget))
                await _attachments.UpsertAsync(newAttachment, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            newAttachment = Attachment.Create(
                tenantId: prior.TenantId,
                storageRef: StorageRef.ForInline(bytes),
                contentHash: hash,
                mimeType: mimeType,
                sizeBytes: bytes.Length,
                originalFilename: originalFilename,
                createdBy: updatedBy,
                sensitivity: sensitivity ?? prior.Sensitivity) with
            {
                ReplacesAttachmentId = prior.Id,
            };
            await _attachments.UpsertAsync(newAttachment, cancellationToken).ConfigureAwait(false);
        }

        // Flip the prior row to Superseded + back-fill ReplacedByAttachmentId.
        var now = Instant.Now;
        var supersededPrior = prior with
        {
            Status = AttachmentStatus.Superseded,
            ReplacedByAttachmentId = newAttachment.Id,
            UpdatedAtUtc = now,
            UpdatedBy = updatedBy,
            Version = prior.Version + 1,
        };
        await _attachments.UpsertAsync(supersededPrior, cancellationToken).ConfigureAwait(false);

        return newAttachment;
    }

    /// <summary>Compute the lowercase-hex sha-256 of a byte span.</summary>
    internal static string ComputeSha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexStringLower(hash);
    }
}
