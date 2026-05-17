using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    // ----- SE-1 council blocker: SupersedeAsync must run sniff → sanitize → policy. -----

    [Fact]
    public async Task Supersede_NonWhitelistedSniff_Throws()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        // First upload a benign PDF (passes all gates).
        var prior = await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf", "innocent.pdf", "u");

        // Now attempt to supersede with a Windows-PE payload claiming to be PDF.
        // Pre-amendment this slipped through; post-amendment it must throw.
        var exe = "MZ\x90\x00executable-payload-here"u8.ToArray();
        var ex = await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.SupersedeAsync(prior.Id, exe, "application/pdf", "still-innocent.pdf", "u"));
        Assert.Equal(PolicyRejection.Mime, ex.RejectionReason);
    }

    [Fact]
    public async Task Supersede_DangerousFilename_Sanitized()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        var prior = await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf", "first.pdf", "u");

        // Replace with a distinct payload + traversal filename — sanitize must strip components.
        var secondPdf = "%PDF-1.4\nrevised version"u8.ToArray();
        var replaced = await svc.SupersedeAsync(prior.Id, secondPdf, "application/pdf",
            originalFilename: "../../../etc/passwd.pdf", updatedBy: "u");

        Assert.Equal("passwd.pdf", replaced.OriginalFilename);
        // Sniffed MIME, not caller hint, persisted.
        Assert.Equal("application/pdf", replaced.MimeType);
    }

    [Fact]
    public async Task Supersede_OversizePayload_Throws()
    {
        var repo = new InMemoryAttachmentRepository();
        var options = new BlocksDocsOptions { MaxAttachmentBytes = 100 };
        var policy = new MimeTypeAndSizePolicy(options, repo);
        var svc = new AttachmentService(repo, policy);

        // Initial upload (small) passes.
        var prior = await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf", "small.pdf", "u");

        // 200-byte payload exceeds the 100-byte per-attachment cap.
        var big = new byte[200];
        "%PDF-1.4"u8.CopyTo(big);
        var ex = await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.SupersedeAsync(prior.Id, big, "application/pdf", "big.pdf", "u"));
        Assert.Equal(PolicyRejection.Size, ex.RejectionReason);
    }

    // ----- SE-4 council amendment: rejections emit a structured audit-log entry. -----

    [Fact]
    public async Task Upload_RejectedByPolicy_EmitsAuditLogEntry()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var capturingLogger = new CapturingLogger<AttachmentService>();
        var svc = new AttachmentService(repo, policy, capturingLogger);

        var exe = "MZ\x90\x00payload"u8.ToArray();
        await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.UploadAsync(Tenant(), exe, "application/pdf", "innocent.pdf", "u"));

        Assert.Single(capturingLogger.Entries);
        var entry = capturingLogger.Entries[0];
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("rejected", entry.Message, StringComparison.Ordinal);
        // PII guard: full filename must NOT appear; the extension may.
        Assert.DoesNotContain("innocent.pdf", entry.Message);
    }

    [Fact]
    public async Task Supersede_RejectedByPolicy_EmitsAuditLogEntry()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var capturingLogger = new CapturingLogger<AttachmentService>();
        var svc = new AttachmentService(repo, policy, capturingLogger);

        var prior = await svc.UploadAsync(Tenant(), PdfBytes, "application/pdf", "first.pdf", "u");
        capturingLogger.Entries.Clear(); // discard any upload-path noise

        var exe = "MZ\x90\x00payload"u8.ToArray();
        await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.SupersedeAsync(prior.Id, exe, "application/pdf", "innocent.pdf", "u"));

        Assert.Single(capturingLogger.Entries);
        Assert.Contains("supersede", capturingLogger.Entries[0].Message, StringComparison.Ordinal);
    }

    // ----- SE-6 council amendment: UploadRejectedException carries tenant-id internally only. -----

    [Fact]
    public async Task UploadRejectedException_DoesNotLeakTenantIdInMessage_ButCarriesItInternally()
    {
        var repo = new InMemoryAttachmentRepository();
        var policy = new MimeTypeAndSizePolicy(new BlocksDocsOptions(), repo);
        var svc = new AttachmentService(repo, policy);

        var exe = "MZ\x90\x00payload"u8.ToArray();
        var ex = await Assert.ThrowsAsync<UploadRejectedException>(() =>
            svc.UploadAsync(new TenantId("secret-tenant"), exe, "application/pdf", "x.pdf", "u"));

        // Public surface: no tenant id.
        Assert.DoesNotContain("secret-tenant", ex.Message);
        Assert.DoesNotContain("secret-tenant", ex.Detail);
        // Internal surface: tenant id preserved for audit-log correlation.
        Assert.Equal(new TenantId("secret-tenant"), ex.TenantIdInternal);
    }

    // ----- Test-only capturing logger so audit assertions can inspect entries. -----

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));

        public sealed record LogEntry(LogLevel Level, string Message);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
