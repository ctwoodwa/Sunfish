using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>SHA-256-based hash-chain primitives for audit records.</summary>
public static class HashChain
{
    /// <summary>
    /// Computes the hash of an audit record given the previous record's hash.
    /// </summary>
    /// <remarks>
    /// Input = <c>prevHash || EntityId || Op || Actor || Tenant || At(O) || canonical(Payload)</c>.
    /// Output: hex-encoded lowercase SHA-256.
    /// </remarks>
    public static string ComputeHash(
        string? prevHash,
        EntityId entityId,
        Op op,
        ActorId actor,
        TenantId tenant,
        DateTimeOffset at,
        JsonDocument payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var prefix = prevHash ?? string.Empty;
        var payloadCanonical = JsonCanonicalizer.ToCanonicalString(payload);
        var input = $"{prefix}|{entityId}|{(int)op}|{actor.Value}|{tenant.Value}|{at:O}|{payloadCanonical}";
        var bytes = Encoding.UTF8.GetBytes(input);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(bytes, digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>
    /// Verifies an ordered sequence of audit records against their hash chain.
    /// </summary>
    /// <param name="orderedByAt">Records ordered by <see cref="AuditRecord.At"/> ascending.</param>
    /// <returns><c>true</c> if every record's <c>Prev</c> link and hash line up; otherwise <c>false</c>.</returns>
    public static bool Verify(IReadOnlyList<AuditRecord> orderedByAt)
    {
        ArgumentNullException.ThrowIfNull(orderedByAt);
        string? previousHash = null;
        AuditId? previousId = null;
        foreach (var record in orderedByAt)
        {
            if (record.Prev != previousId) return false;
            var expected = ComputeHash(
                previousHash,
                record.EntityId,
                record.Op,
                record.Actor,
                record.Tenant,
                record.At,
                record.Payload);
            if (!string.Equals(expected, record.Hash, StringComparison.Ordinal))
                return false;
            previousHash = record.Hash;
            previousId = record.Id;
        }
        return true;
    }
}
