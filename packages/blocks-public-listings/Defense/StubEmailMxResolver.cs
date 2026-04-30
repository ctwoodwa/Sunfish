using System.Collections.Concurrent;

namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// Test/demo <see cref="IEmailMxResolver"/>. Domains seeded via
/// <see cref="Seed(string, bool)"/> return the seeded verdict;
/// unseeded domains default to <see cref="DefaultVerdict"/>. Production
/// deployments wire a real DNS-backed resolver instead.
/// </summary>
public sealed class StubEmailMxResolver : IEmailMxResolver
{
    private readonly ConcurrentDictionary<string, bool> _seeds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Verdict returned for domains not seeded via <see cref="Seed"/>.</summary>
    public bool DefaultVerdict { get; init; } = true;

    /// <summary>Pre-seeds a domain with an MX-presence verdict.</summary>
    public void Seed(string domain, bool hasMx)
    {
        ArgumentException.ThrowIfNullOrEmpty(domain);
        _seeds[domain] = hasMx;
    }

    /// <inheritdoc />
    public Task<bool> HasMxRecordAsync(string domain, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(domain);
        ct.ThrowIfCancellationRequested();
        if (_seeds.TryGetValue(domain, out var verdict))
        {
            return Task.FromResult(verdict);
        }
        return Task.FromResult(DefaultVerdict);
    }
}
