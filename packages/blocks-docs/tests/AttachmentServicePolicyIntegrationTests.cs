using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class AttachmentServicePolicyIntegrationTests
{
    private static TenantId Tenant() => new("acme");

    // Minimum-valid PDF magic so the sniffer recognizes the upload.
    private static readonly byte[] PdfBytes = "%PDF-1.4\nminimal-test-pdf"u8.ToArray();

    [Fact]
    public async Task Upload_PolicyAccepts_PersistsAttachmentWithSniffedMime()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        var a = await svc.UploadAsync(Tenant(), PdfBytes, "ignored/by-sniffer", "report.pdf", "user-1");

        // Persisted MIME is the sniffed value, not the caller's hint.
        Assert.Equal("application/pdf", a.MimeType);
    }

    [Fact]
    public async Task Upload_DangerousFilename_SanitizedToLeaf()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        var a = await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf",
            originalFilename: "../../../etc/passwd.pdf",
            createdBy: "u");

        Assert.Equal("passwd.pdf", a.OriginalFilename);
    }

    [Fact]
    public async Task Upload_RejectedFilename_FallsBackToSafeDefault()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        var a = await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf",
            originalFilename: "CON.pdf",   // Windows reserved → sanitizer returns null
            createdBy: "u");

        Assert.Equal("attachment.bin", a.OriginalFilename);
    }

    [Fact]
    public async Task Upload_NonWhitelistedSniff_ThrowsUploadRejectedException_WithMimeReason()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        var exe = "MZ\x90\x00executable"u8.ToArray();
        var ex = await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.UploadAsync(Tenant(), exe, "application/pdf", "innocent.pdf", "u"));
        Assert.Equal(PolicyRejection.Mime, ex.RejectionReason);
    }

    [Fact]
    public async Task Upload_OversizePayload_ThrowsUploadRejectedException_WithSizeReason()
    {
        var repo = new InMemoryAttachmentRepository();
        var options = new BlocksDocsOptions { MaxAttachmentBytes = 100 };
        var policy = new MimeTypeAndSizePolicy(options, repo);
        var svc = new AttachmentService(repo, policy);

        // 200-byte PDF (still has magic, but exceeds the 100-byte cap)
        var bigPdf = new byte[200];
        "%PDF-1.4"u8.CopyTo(bigPdf);

        var ex = await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.UploadAsync(Tenant(), bigPdf, "application/pdf", "big.pdf", "u"));
        Assert.Equal(PolicyRejection.Size, ex.RejectionReason);
    }

    [Fact]
    public async Task Upload_TenantQuotaExceeded_ThrowsUploadRejectedException_WithTenantQuotaReason()
    {
        var repo = new InMemoryAttachmentRepository();
        var options = new BlocksDocsOptions
        {
            TenantQuotaBytes = new Dictionary<string, long?>
            {
                ["acme"] = 30,
            },
        };
        var policy = new MimeTypeAndSizePolicy(options, repo);
        var svc = new AttachmentService(repo, policy);

        // First upload (size = PdfBytes.Length, ≈ 25 bytes) succeeds.
        await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf", "first.pdf", "u");

        // Second upload pushes total over the 30-byte quota.
        var secondPdf = "%PDF-1.4\nanother test"u8.ToArray(); // distinct bytes → no dedup
        var ex = await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.UploadAsync(Tenant(), secondPdf, "application/pdf", "second.pdf", "u"));
        Assert.Equal(PolicyRejection.TenantQuota, ex.RejectionReason);
    }

    [Fact]
    public async Task Upload_NullPolicy_FallsThroughToPR2Behavior()
    {
        // Service constructed without a policy (test-fixture path): the
        // sniffer + sanitizer still run, but no gate rejects.
        var repo = new InMemoryAttachmentRepository();
        var svc = new AttachmentService(repo);

        var anyBytes = "MZ\x90\x00would-fail-policy"u8.ToArray();
        var a = await svc.UploadAsync(Tenant(), anyBytes, "irrelevant", "renamed.pdf", "u");
        Assert.NotNull(a);
        // Sniffed MIME is still persisted (sniffing happens regardless).
        Assert.Equal("application/x-msdownload", a.MimeType);
    }

    [Fact]
    public async Task GetTenantTotalSizeBytes_SumsOnlyActiveRows()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        var a = await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf", "a.pdf", "u");
        Assert.Equal(PdfBytes.Length, await repo.GetTenantTotalSizeBytesAsync(Tenant()));

        // Tombstone the attachment — total drops to 0.
        await repo.SoftDeleteAsync(a.Id, "u", "test");
        Assert.Equal(0, await repo.GetTenantTotalSizeBytesAsync(Tenant()));
    }
}
