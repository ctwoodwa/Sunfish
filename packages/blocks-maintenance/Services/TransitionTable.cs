namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Lightweight helper that encodes a set of allowed state transitions and throws a descriptive
/// <see cref="InvalidOperationException"/> when a transition is forbidden.
/// </summary>
/// <typeparam name="TState">An enum type representing lifecycle states.</typeparam>
internal sealed class TransitionTable<TState> where TState : struct, Enum
{
    private readonly Dictionary<TState, HashSet<TState>> _allowed;

    /// <summary>
    /// Initialises the table from a sequence of (from, to[]) pairs.
    /// </summary>
    public TransitionTable(IEnumerable<(TState From, TState[] To)> rules)
    {
        _allowed = new Dictionary<TState, HashSet<TState>>();
        foreach (var (from, targets) in rules)
            _allowed[from] = [.. targets];
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if transitioning from
    /// <paramref name="current"/> to <paramref name="next"/> is not permitted.
    /// </summary>
    public void Guard(TState current, TState next, string entityLabel)
    {
        if (_allowed.TryGetValue(current, out var allowed) && allowed.Contains(next))
            return;

        throw new InvalidOperationException(
            $"Cannot transition {entityLabel} from {current} to {next}. " +
            $"Allowed targets from {current}: [{string.Join(", ", _allowed.TryGetValue(current, out var a) ? a : [])}].");
    }
}
