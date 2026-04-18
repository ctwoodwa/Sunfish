using Sunfish.Federation.EntitySync.Protocol;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.EntitySync.Tests;

/// <summary>Shared helpers for constructing signed change records in tests.</summary>
internal static class TestData
{
    public static EntityId NewEntity(string localPart)
        => new("test", "authority", localPart);

    public static VersionId NewVersion(EntityId entity, int sequence)
        => new(entity, sequence, Guid.NewGuid().ToString("N"));

    public static SignedOperation<ChangeRecord> NewSigned(
        Ed25519Signer signer,
        EntityId entity,
        int sequence,
        VersionId? parent = null,
        byte[]? diff = null)
    {
        var version = NewVersion(entity, sequence);
        var change = new ChangeRecord(
            entity,
            version,
            parent,
            DateTimeOffset.UtcNow,
            diff ?? new byte[] { 0xAA });
        return signer.SignAsync(change, DateTimeOffset.UtcNow, Guid.NewGuid()).AsTask().GetAwaiter().GetResult();
    }
}
