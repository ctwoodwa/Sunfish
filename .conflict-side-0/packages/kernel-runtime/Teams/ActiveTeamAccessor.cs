namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Default <see cref="IActiveTeamAccessor"/>. Thread-safe via a gate on mutation;
/// event invocation happens outside the lock. Per ADR 0032.
/// </summary>
public sealed class ActiveTeamAccessor : IActiveTeamAccessor
{
    private readonly ITeamContextFactory _factory;
    private readonly object _gate = new();
    private TeamContext? _active;

    /// <summary>Construct an accessor over <paramref name="factory"/>.</summary>
    public ActiveTeamAccessor(ITeamContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public TeamContext? Active
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;

    /// <inheritdoc />
    public Task SetActiveAsync(TeamId teamId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Resolve against the factory's live set. Do not auto-materialize — the
        // contract is "teams must already be joined (materialized) before they
        // can be made active" per ADR 0032.
        var target = _factory.Active.FirstOrDefault(c => c.TeamId.Equals(teamId));
        if (target is null)
        {
            throw new InvalidOperationException(
                $"Team {teamId} is not materialized. Call ITeamContextFactory.GetOrCreateAsync first.");
        }

        TeamContext? previous;
        bool changed;
        lock (_gate)
        {
            previous = _active;
            changed = !ReferenceEquals(previous, target);
            _active = target;
        }

        if (changed)
        {
            ActiveChanged?.Invoke(this, new ActiveTeamChangedEventArgs(previous, target));
        }
        return Task.CompletedTask;
    }
}
