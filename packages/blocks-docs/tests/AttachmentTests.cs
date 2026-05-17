using System.Text.Json;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class AttachmentTests
{
    private static TenantId Tenant() => new("acme");
    private static StorageRef Foundation() => StorageRef.ForFoundationBlob("bafy-test-cid");

    [Fact]
    public void Create_HappyPath_PopulatesAllRequiredFieldsAndDefaults()
    {
        var a = Attachment.Create(
            tenantId: Tenant(),
            storageRef: Foundation(),
            contentHash: "abc123",
            mimeType: "application/pdf",
            sizeBytes: 12345,
            originalFilename: "receipt.pdf",
            createdBy: "user-1");

        Assert.Equal("abc123", a.ContentHash);
        Assert.Equal("application/pdf", a.MimeType);
        Assert.Equal(12345, a.SizeBytes);
        Assert.Equal("receipt.pdf", a.OriginalFilename);
        Assert.Equal(AttachmentStatus.Active, a.Status);
        Assert.Equal(Sensitivity.Internal, a.Sensitivity); // default
        Assert.Equal(1, a.Version);
        Assert.Null(a.ThumbnailRef);
        Assert.Null(a.ReplacesAttachmentId);
        Assert.Null(a.ReplacedByAttachmentId);
        Assert.Empty(a.RevisionVector);
    }

    [Fact]
    public void Create_NullStorageRef_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Attachment.Create(Tenant(), null!, "h", "m/t", 1, "f"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankContentHash_Throws(string hash)
    {
        Assert.Throws<ArgumentException>(() =>
            Attachment.Create(Tenant(), Foundation(), hash, "m/t", 1, "f"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankMimeType_Throws(string mime)
    {
        Assert.Throws<ArgumentException>(() =>
            Attachment.Create(Tenant(), Foundation(), "h", mime, 1, "f"));
    }

    [Fact]
    public void Create_NegativeSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Attachment.Create(Tenant(), Foundation(), "h", "m/t", -1, "f"));
    }

    [Fact]
    public void Create_ZeroSize_Allowed()
    {
        var a = Attachment.Create(Tenant(), Foundation(), "h", "m/t", 0, "f");
        Assert.Equal(0, a.SizeBytes);
    }

    [Fact]
    public void Create_AcceptsExplicitSensitivity()
    {
        var a = Attachment.Create(Tenant(), Foundation(), "h", "m/t", 1, "f", sensitivity: Sensitivity.Pii);
        Assert.Equal(Sensitivity.Pii, a.Sensitivity);
    }

    [Fact]
    public void AttachmentId_JsonRoundtrip_PreservesValue()
    {
        var id = AttachmentId.NewId();
        var json = JsonSerializer.Serialize(id);
        Assert.Equal(id, JsonSerializer.Deserialize<AttachmentId>(json));
    }

    [Fact]
    public void Sensitivity_JsonRoundtrip_LowercaseCodes()
    {
        Assert.Equal("\"internal\"",     JsonSerializer.Serialize(Sensitivity.Internal));
        Assert.Equal("\"pii\"",          JsonSerializer.Serialize(Sensitivity.Pii));
        Assert.Equal("\"financial\"",    JsonSerializer.Serialize(Sensitivity.Financial));
        Assert.Equal("\"confidential\"", JsonSerializer.Serialize(Sensitivity.Confidential));

        Assert.Equal(Sensitivity.Pii, JsonSerializer.Deserialize<Sensitivity>("\"pii\""));
    }

    [Fact]
    public void Sensitivity_UnknownJson_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Sensitivity>("\"top-secret\""));
    }
}
