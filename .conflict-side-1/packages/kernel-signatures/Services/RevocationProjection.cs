using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// Pure projection of an append-only revocation log into a current-validity
/// verdict per ADR 0054 amendments A4 + A5. Concurrent revocations under
/// the AP / CRDT model (ADR 0028) merge by **last-revocation-wins**:
/// the entry with the latest <see cref="SignatureRevocation.RevokedAt"/>
/// in partial order is the winner; ties are broken by total order on
/// <see cref="RevocationEventId.Value"/> (Guid byte order).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why pure:</b> the merge rule is the one piece of revocation logic
/// that storage backends MUST implement identically — InMemory, EFCore,
/// CRDT-replicated, or otherwise. Extracting it as a static pure
/// function lets every implementation delegate without subtle
/// behavioral drift, and lets tests exercise concurrent-revocation
/// scenarios without spinning up an <see cref="ISignatureRevocationLog"/>
/// instance.
/// </para>
/// <para>
/// <b>Idempotence note:</b> duplicate revocation entries (same
/// <see cref="RevocationEventId"/>) MUST be deduped at the storage
/// layer. <see cref="Project"/> assumes its input contains at most one
/// entry per <c>RevocationEventId</c>; if duplicates do reach the
/// projection, the result is well-defined (idempotent) but
/// <see cref="ISignatureRevocationLog"/> implementations should still
/// dedup at append time.
/// </para>
/// </remarks>
public static class RevocationProjection
{
    /// <summary>
    /// Computes the current validity status for a signature event from a
    /// snapshot of revocation entries. Empty input → valid. Non-empty
    /// input → invalid, with <see cref="SignatureValidityStatus.RevokedBy"/>
    /// set to the winning entry per the last-revocation-wins rule.
    /// </summary>
    /// <param name="entries">Snapshot of revocation entries; ordering is unimportant.</param>
    public static SignatureValidityStatus Project(IEnumerable<SignatureRevocation> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        SignatureRevocation? winning = null;
        foreach (var entry in entries)
        {
            if (winning is null || ComparePartialOrder(entry, winning) > 0)
            {
                winning = entry;
            }
        }

        return winning is null
            ? new SignatureValidityStatus { IsValid = true }
            : new SignatureValidityStatus { IsValid = false, RevokedBy = winning };
    }

    /// <summary>
    /// Comparator implementing the partial order: latest <c>RevokedAt</c>
    /// wins; ties resolve by total-order on <c>RevocationEventId.Value</c>
    /// (Guid byte sequence). Returns positive when <paramref name="a"/>
    /// should win, negative when <paramref name="b"/> should win,
    /// zero only when both have the same <c>RevocationEventId</c>
    /// (storage-layer dedup invariant).
    /// </summary>
    public static int ComparePartialOrder(SignatureRevocation a, SignatureRevocation b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var byTime = a.RevokedAt.CompareTo(b.RevokedAt);
        if (byTime != 0)
        {
            return byTime;
        }
        // Tie on RevokedAt — fall through to total-order on the Guid
        // byte sequence. Guid.CompareTo is stable + transitive.
        return a.Id.Value.CompareTo(b.Id.Value);
    }
}
