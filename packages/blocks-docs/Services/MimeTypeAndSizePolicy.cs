using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Default <see cref="IMimeTypeAndSizePolicy"/>. Runs the three checks
/// in the order MIME → Size → Tenant-Quota — the cheapest checks first
/// so a malicious upload is rejected before the repository is hit for
/// the quota query.
///
/// <para>
/// <b>Council review focus — quota check:</b> the tenant-quota gate
/// fires <i>before</i> bytes are persisted. The repository's
/// <c>GetTenantTotalSizeBytesAsync</c> reads the current cumulative
/// size; we add the proposed upload's size to that figure and reject
/// if the sum exceeds the tenant's quota. This is intentionally a
/// best-effort check, not a hard transactional guarantee — under
/// extreme concurrency two uploads might both pass the gate and
/// jointly exceed the quota by one upload's worth. Hosting agents that
/// need a hard guarantee should run AP/AR/Docs writes through a
/// serializing transaction at the persistence layer.
/// </para>
/// </summary>
public sealed class MimeTypeAndSizePolicy : IMimeTypeAndSizePolicy
{
    private readonly BlocksDocsOptions _options;
    private readonly IAttachmentRepository _repository;

    public MimeTypeAndSizePolicy(BlocksDocsOptions options, IAttachmentRepository repository)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<PolicyResult> ValidateAsync(
        TenantId tenantId,
        string sniffedMime,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        // Pre-gate (SE-2 council blocker): system blacklist.
        // No tenant whitelist override may re-enable an XSS / executable
        // vector. Enforced ahead of the per-tenant lookup.
        if (DefaultMimeWhitelist.SystemBlacklist.Contains(sniffedMime))
        {
            return PolicyResult.Reject(PolicyRejection.Mime,
                $"MIME type '{sniffedMime}' is system-blacklisted and cannot be whitelisted per-tenant.");
        }

        // Gate 1: per-tenant MIME whitelist.
        var allowed = _options.GetAllowedMimeTypes(tenantId.Value);
        if (!allowed.Contains(sniffedMime))
        {
            // SE-6 council amendment: do NOT leak tenant id in rejection detail.
            // The exception type + PolicyRejection enum carry the actionable
            // signal; tenant scope is recovered from the audit-log entry.
            return PolicyResult.Reject(PolicyRejection.Mime,
                $"MIME type '{sniffedMime}' is not whitelisted.");
        }

        // Gate 2: per-attachment size cap.
        var maxAttachment = _options.GetMaxAttachmentBytes(tenantId.Value);
        if (sizeBytes > maxAttachment)
        {
            return PolicyResult.Reject(PolicyRejection.Size,
                $"Upload size {sizeBytes} exceeds per-attachment cap {maxAttachment}.");
        }

        // Gate 3: cumulative tenant quota.
        var quota = _options.GetTenantQuotaBytes(tenantId.Value);
        if (quota is long limit)
        {
            var currentTotal = await _repository.GetTenantTotalSizeBytesAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (currentTotal + sizeBytes > limit)
            {
                return PolicyResult.Reject(PolicyRejection.TenantQuota,
                    $"Tenant quota would be exceeded: current {currentTotal} + upload {sizeBytes} > limit {limit}.");
            }
        }

        return PolicyResult.Accept();
    }
}
