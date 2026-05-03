using Sunfish.Kernel.Security.Attestation;

namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Default in-memory <see cref="IBucketRegistry"/>. Thread-safe for concurrent reads
/// and occasional registrations — registration is expected to happen at startup from
/// a YAML file, not on the hot path.
/// </summary>
public sealed class BucketRegistry : IBucketRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, BucketDefinition> _byName = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<BucketDefinition> Definitions
    {
        get
        {
            lock (_gate)
            {
                return _byName.Values.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public void Register(BucketDefinition bucket)
    {
        ArgumentNullException.ThrowIfNull(bucket);

        lock (_gate)
        {
            if (_byName.ContainsKey(bucket.Name))
            {
                throw new InvalidOperationException(
                    $"A bucket named '{bucket.Name}' is already registered.");
            }
            _byName.Add(bucket.Name, bucket);
        }
    }

    /// <inheritdoc />
    public BucketDefinition? Find(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        lock (_gate)
        {
            return _byName.TryGetValue(name, out var def) ? def : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BucketDefinition> EligibleBucketsFor(IReadOnlyList<RoleAttestation> peerAttestations)
    {
        ArgumentNullException.ThrowIfNull(peerAttestations);

        // Build the role set once — attestations are typically few but bucket definitions may be many.
        var presentedRoles = new HashSet<string>(
            peerAttestations.Where(a => !string.IsNullOrEmpty(a.Role)).Select(a => a.Role),
            StringComparer.Ordinal);

        if (presentedRoles.Count == 0)
        {
            return Array.Empty<BucketDefinition>();
        }

        lock (_gate)
        {
            return _byName.Values
                .Where(def => presentedRoles.Contains(def.RequiredAttestation))
                .ToArray();
        }
    }
}
