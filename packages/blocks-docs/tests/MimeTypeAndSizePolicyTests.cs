using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class MimeTypeAndSizePolicyTests
{
    private static TenantId Tenant(string value = "acme") => new(value);

    [Fact]
    public async Task ValidateAsync_DefaultPolicy_AllowsWhitelistedMime()
    {
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), new InMemoryAttachmentRepository());

        var result = await policy.ValidateAsync(Tenant(), "application/pdf", sizeBytes: 1024);
        Assert.True(result.IsAccepted);
    }

    [Fact]
    public async Task ValidateAsync_NonWhitelistedMime_RejectsWithMimeReason()
    {
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), new InMemoryAttachmentRepository());

        var result = await policy.ValidateAsync(Tenant(), "application/x-msdownload", sizeBytes: 1024);
        Assert.True(result.Rejected);
        Assert.Equal(PolicyRejection.Mime, result.RejectionReason);
    }

    [Fact]
    public async Task ValidateAsync_UnknownMime_RejectsByDefault()
    {
        // application/octet-stream is the sniffer fallback. Default
        // whitelist excludes it; this is the council-mandated
        // detection-fails-deny posture.
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), new InMemoryAttachmentRepository());
        var result = await policy.ValidateAsync(Tenant(), MimeSniffer.UnknownMime, sizeBytes: 100);
        Assert.True(result.Rejected);
        Assert.Equal(PolicyRejection.Mime, result.RejectionReason);
    }

    [Fact]
    public async Task ValidateAsync_OversizeAttachment_RejectsWithSizeReason()
    {
        var options = new BlocksDocsOptions { MaxAttachmentBytes = 1_000 };
        var policy = new MimeTypeAndSizePolicy(options, new InMemoryAttachmentRepository());

        var result = await policy.ValidateAsync(Tenant(), "application/pdf", sizeBytes: 1_001);
        Assert.True(result.Rejected);
        Assert.Equal(PolicyRejection.Size, result.RejectionReason);
    }

    [Fact]
    public async Task ValidateAsync_PerTenantSizeCapOverride_Applies()
    {
        var options = new BlocksDocsOptions
        {
            MaxAttachmentBytes = 100_000,
            MaxAttachmentBytesPerTenant = new Dictionary<string, long>
            {
                ["strict-tenant"] = 1_000,
            },
        };
        var policy = new MimeTypeAndSizePolicy(options, new InMemoryAttachmentRepository());

        // strict-tenant: 1_000 cap
        Assert.True((await policy.ValidateAsync(new TenantId("strict-tenant"), "application/pdf", 1_001)).Rejected);
        // other tenant: 100_000 cap
        Assert.False((await policy.ValidateAsync(Tenant("other"), "application/pdf", 50_000)).Rejected);
    }

    [Fact]
    public async Task ValidateAsync_TenantQuotaWouldBeExceeded_RejectsWithQuotaReason()
    {
        var repo = new InMemoryAttachmentRepository();
        // Pre-seed an existing attachment of 800 bytes.
        var seeded = Attachment.Create(
            tenantId: Tenant(),
            storageRef: StorageRef.ForFoundationBlob("cid-existing"),
            contentHash: "existing",
            mimeType: "application/pdf",
            sizeBytes: 800,
            originalFilename: "old.pdf",
            createdBy: "u");
        await repo.UpsertAsync(seeded);

        var options = new BlocksDocsOptions
        {
            TenantQuotaBytes = new Dictionary<string, long?>
            {
                ["acme"] = 1_000,
            },
        };
        var policy = new MimeTypeAndSizePolicy(options, repo);

        // 800 existing + 300 new = 1_100 > 1_000 → reject
        var result = await policy.ValidateAsync(Tenant(), "application/pdf", sizeBytes: 300);
        Assert.True(result.Rejected);
        Assert.Equal(PolicyRejection.TenantQuota, result.RejectionReason);
    }

    [Fact]
    public async Task ValidateAsync_NoTenantQuota_DoesNotCheckRepo()
    {
        var options = new BlocksDocsOptions(); // no quota dict entries
        var policy = new MimeTypeAndSizePolicy(options, new InMemoryAttachmentRepository());

        var result = await policy.ValidateAsync(Tenant(), "application/pdf", sizeBytes: long.MaxValue / 2);
        // No quota → only the size-cap gate applies. Default 100 MB cap < long.MaxValue/2 → reject by size.
        Assert.Equal(PolicyRejection.Size, result.RejectionReason);
    }

    [Fact]
    public async Task ValidateAsync_PerTenantMimeWhitelistOverride_Applies()
    {
        var customWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/x-custom-format",
        };
        var options = new BlocksDocsOptions
        {
            MimeWhitelistPerTenant = new Dictionary<string, IReadOnlySet<string>>
            {
                ["lenient-tenant"] = customWhitelist,
            },
        };
        var policy = new MimeTypeAndSizePolicy(options, new InMemoryAttachmentRepository());

        // lenient-tenant: accepts custom MIME
        Assert.False((await policy.ValidateAsync(new TenantId("lenient-tenant"), "application/x-custom-format", 100)).Rejected);
        // lenient-tenant: rejects default-whitelisted MIME (the override is total replacement, not append)
        Assert.True((await policy.ValidateAsync(new TenantId("lenient-tenant"), "application/pdf", 100)).Rejected);
        // other tenant: default whitelist applies
        Assert.False((await policy.ValidateAsync(Tenant("other"), "application/pdf", 100)).Rejected);
    }

    // SE-2 council blocker: tenant whitelist cannot re-enable a system-blacklisted MIME.
    [Theory]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    [InlineData("text/javascript")]
    [InlineData("application/x-msdownload")]
    [InlineData("application/x-executable")]
    [InlineData("application/x-sh")]
    [InlineData("application/octet-stream")]
    [InlineData("application/x-shockwave-flash")]
    public async Task ValidateAsync_TenantWhitelistsBlacklistedMime_StillRejected(string blacklisted)
    {
        // Construct a tenant whose override INCLUDES every system-blacklisted MIME.
        var customWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            blacklisted,
        };
        var options = new BlocksDocsOptions
        {
            MimeWhitelistPerTenant = new Dictionary<string, IReadOnlySet<string>>
            {
                ["misconfigured-tenant"] = customWhitelist,
            },
        };
        var policy = new MimeTypeAndSizePolicy(options, new InMemoryAttachmentRepository());

        var result = await policy.ValidateAsync(new TenantId("misconfigured-tenant"), blacklisted, sizeBytes: 100);
        Assert.True(result.Rejected, $"system blacklist must trump tenant whitelist for {blacklisted}");
        Assert.Equal(PolicyRejection.Mime, result.RejectionReason);
        // SE-6: the rejection detail must not echo the tenant id.
        Assert.DoesNotContain("misconfigured-tenant", result.Detail);
    }

    // SE-6 council amendment: rejection details must not leak tenant id.
    [Fact]
    public async Task ValidateAsync_AnyRejectionPath_DetailDoesNotContainTenantId()
    {
        var repo = new InMemoryAttachmentRepository();
        var seeded = Attachment.Create(
            tenantId: new TenantId("secret-tenant"),
            storageRef: StorageRef.ForFoundationBlob("cid-x"),
            contentHash: "h",
            mimeType: "application/pdf",
            sizeBytes: 950,
            originalFilename: "x.pdf",
            createdBy: "u");
        await repo.UpsertAsync(seeded);

        var options = new BlocksDocsOptions
        {
            MaxAttachmentBytes = 100,
            TenantQuotaBytes = new Dictionary<string, long?> { ["secret-tenant"] = 1_000 },
        };
        var policy = new MimeTypeAndSizePolicy(options, repo);

        // Each gate, in turn — none should mention "secret-tenant" in Detail.
        var mimeReject = await policy.ValidateAsync(new TenantId("secret-tenant"), "application/x-msdownload", 50);
        Assert.DoesNotContain("secret-tenant", mimeReject.Detail);

        var sizeReject = await policy.ValidateAsync(new TenantId("secret-tenant"), "application/pdf", 500);
        Assert.DoesNotContain("secret-tenant", sizeReject.Detail);

        // 950 seeded + 99 new = 1_049 > 1_000 quota → quota rejection.
        var quotaReject = await policy.ValidateAsync(new TenantId("secret-tenant"), "application/pdf", 99);
        Assert.DoesNotContain("secret-tenant", quotaReject.Detail);
        Assert.Equal(PolicyRejection.TenantQuota, quotaReject.RejectionReason);
    }
}
