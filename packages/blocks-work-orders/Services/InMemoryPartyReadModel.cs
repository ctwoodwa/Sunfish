using System.Collections.Concurrent;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// In-memory <see cref="IPartyReadModel"/> for tests + the
/// kitchen-sink demo. Always returns <c>null</c> for unknown ids;
/// callers can seed display names via <see cref="Seed"/>. Disappears
/// when <c>blocks-people-foundation</c> ships its canonical impl.
/// </summary>
// TODO: relocate to Sunfish.Blocks.People.Foundation when that package ships.
public sealed class InMemoryPartyReadModel : IPartyReadModel
{
    private readonly ConcurrentDictionary<Guid, string> _displayNames = new();

    /// <summary>Seed (or replace) a party's display name.</summary>
    public void Seed(Guid partyId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName must be non-empty.", nameof(displayName));
        _displayNames[partyId] = displayName;
    }

    /// <inheritdoc />
    public Task<string?> GetDisplayNameAsync(Guid partyId, CancellationToken cancellationToken = default)
        => Task.FromResult(_displayNames.TryGetValue(partyId, out var name) ? name : null);
}
