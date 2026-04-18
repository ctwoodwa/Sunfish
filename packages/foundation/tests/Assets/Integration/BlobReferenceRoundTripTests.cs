using System.Text;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Blobs;

namespace Sunfish.Foundation.Tests.Assets.Integration;

/// <summary>
/// Integration between the Assets primitives and the already-shipped <see cref="Sunfish.Foundation.Blobs"/>
/// primitive: entity bodies carry <see cref="Cid"/> references, not inline bytes.
/// </summary>
public sealed class BlobReferenceRoundTripTests
{
    private static readonly SchemaId Schema = new("document.v1");
    private static readonly ActorId Actor = new("tester");
    private static readonly TenantId Tenant = new("acme");

    [Fact]
    public async Task EntityBody_CarriesCidReference_AndBlobIsReachableByCid()
    {
        // Arrange: put some bytes behind a CID in a Dictionary-backed "blob store".
        var blobs = new Dictionary<string, byte[]>();
        var bytes = Encoding.UTF8.GetBytes("Inspection report PDF bytes");
        var cid = Cid.FromBytes(bytes);
        blobs[cid.Value] = bytes;

        var storage = new InMemoryAssetStorage();
        var entities = new InMemoryEntityStore(storage);

        // Build entity body that references the blob by CID.
        var body = JsonDocument.Parse($$$"""{"title":"Inspection 2024","report":{"cid":"{{{cid.Value}}}"}}""");
        var id = await entities.CreateAsync(Schema, body,
            new CreateOptions("document", "acme", "insp-2024", Actor, Tenant));

        var ent = await entities.GetAsync(id);
        Assert.NotNull(ent);
        using var round = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(ent!.Body.RootElement));
        var cidFromBody = round.RootElement.GetProperty("report").GetProperty("cid").GetString();
        Assert.NotNull(cidFromBody);

        // Follow the CID back to the bytes.
        Assert.True(blobs.ContainsKey(cidFromBody!));
        Assert.Equal(bytes, blobs[cidFromBody!]);
    }

    [Fact]
    public async Task UpdateEntity_ChangingCidReference_TriggersNewEntityVersion_LeavesOldBlobReachable()
    {
        var blobs = new Dictionary<string, byte[]>();
        var bytesV1 = Encoding.UTF8.GetBytes("photo v1");
        var bytesV2 = Encoding.UTF8.GetBytes("photo v2 higher-resolution");
        var cidV1 = Cid.FromBytes(bytesV1);
        var cidV2 = Cid.FromBytes(bytesV2);
        blobs[cidV1.Value] = bytesV1;
        blobs[cidV2.Value] = bytesV2;

        var storage = new InMemoryAssetStorage();
        var entities = new InMemoryEntityStore(storage);

        var id = await entities.CreateAsync(Schema,
            JsonDocument.Parse($$$"""{"photo":{"cid":"{{{cidV1.Value}}}"}}"""),
            new CreateOptions("document", "acme", "doc-1", Actor, Tenant));
        await entities.UpdateAsync(id,
            JsonDocument.Parse($$$"""{"photo":{"cid":"{{{cidV2.Value}}}"}}"""),
            new UpdateOptions(Actor));

        Assert.Equal(2, storage.Versions[id].Count);
        // Old blob still addressable — no GC of blobs.
        Assert.True(blobs.ContainsKey(cidV1.Value));
        Assert.True(blobs.ContainsKey(cidV2.Value));
    }
}
