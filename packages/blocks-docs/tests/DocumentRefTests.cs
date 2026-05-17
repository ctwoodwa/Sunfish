using System.Text.Json;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class DocumentRefTests
{
    private static TenantId Tenant() => new("acme");
    private static AttachmentId SomeAttachment() => AttachmentId.NewId();

    [Fact]
    public void Create_FillsCrdtEnvelope_AndDefaultsLive()
    {
        var aId = SomeAttachment();
        var doc = DocumentRef.Create(
            tenantId: Tenant(),
            attachmentId: aId,
            clusterCode: "blocks-financial-ar",
            parentEntityType: "invoice",
            parentEntityId: "INV-2026-001",
            createdBy: "u",
            attachmentRole: "primary-attachment");

        Assert.Equal(aId, doc.AttachmentId);
        Assert.Equal("blocks-financial-ar", doc.ClusterCode);
        Assert.Equal("invoice", doc.ParentEntityType);
        Assert.Equal("INV-2026-001", doc.ParentEntityId);
        Assert.Equal("primary-attachment", doc.AttachmentRole);
        Assert.Equal("u", doc.CreatedBy);
        Assert.Equal(1, doc.Version);
        Assert.Null(doc.DeletedAtUtc);
        Assert.True(doc.IsLive);
    }

    [Fact]
    public void Create_WithoutAttachmentRole_LeavesItNull()
    {
        var doc = DocumentRef.Create(Tenant(), SomeAttachment(), "blocks-leases", "lease", "L-1", "u");
        Assert.Null(doc.AttachmentRole);
    }

    [Theory]
    [InlineData("", "type", "id")]
    [InlineData(" ", "type", "id")]
    [InlineData("cluster", "", "id")]
    [InlineData("cluster", " ", "id")]
    [InlineData("cluster", "type", "")]
    [InlineData("cluster", "type", " ")]
    public void Create_RequiredFieldsMustBePresent(string cluster, string entityType, string entityId)
    {
        Assert.Throws<ArgumentException>(() =>
            DocumentRef.Create(Tenant(), SomeAttachment(), cluster, entityType, entityId, "u"));
    }

    [Fact]
    public void DocumentRefId_JsonRoundTrip_PreservesValue()
    {
        var id = DocumentRefId.NewId();
        var json = JsonSerializer.Serialize(id);
        Assert.Equal($"\"{id.Value}\"", json);

        var back = JsonSerializer.Deserialize<DocumentRefId>(json);
        Assert.Equal(id, back);
    }

    [Fact]
    public void DocumentRefId_ImplicitConversions_RoundTripCleanly()
    {
        DocumentRefId fromString = "abc-123";
        Assert.Equal("abc-123", fromString.Value);

        string toString = fromString;
        Assert.Equal("abc-123", toString);
    }
}
