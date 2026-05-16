namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Caller's identity + role membership. Used by
/// <see cref="JournalPostingService"/> for period-gating role bypass
/// (the <c>FinancialAdmin</c> role can post to a soft-closed period
/// per Stage 02 §6.1).
/// </summary>
/// <remarks>
/// Local placeholder. A future shared <c>Sunfish.Foundation.Identity</c>
/// or session-context type will replace this. TODO: relocate when the
/// shared type lands.
/// </remarks>
public interface IUserContext
{
    /// <summary>The current user's id (opaque string).</summary>
    string UserId { get; }

    /// <summary><c>true</c> if the current user holds <paramref name="role"/>.</summary>
    bool HasRole(string role);
}

/// <summary>
/// In-memory <see cref="IUserContext"/> for tests and dev-mode bootstrap.
/// </summary>
public sealed class StaticUserContext : IUserContext
{
    private readonly HashSet<string> _roles;

    public StaticUserContext(string userId, IEnumerable<string>? roles = null)
    {
        UserId = userId;
        _roles = new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string UserId { get; }

    /// <inheritdoc />
    public bool HasRole(string role) => _roles.Contains(role);
}
