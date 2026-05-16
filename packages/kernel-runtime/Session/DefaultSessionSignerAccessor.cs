using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Security.Session;

namespace Sunfish.Kernel.Runtime.Session;

/// <summary>
/// Default <see cref="ISessionSignerAccessor"/>. Resolves the active team
/// (<see cref="IActiveTeamAccessor.Active"/>), reads the install root seed
/// (<see cref="IRootSeedProvider.GetRootSeedAsync"/>), derives the team's
/// identity keypair via <see cref="ITeamSubkeyDerivation.DeriveTeamKeypair"/>
/// (ADR 0032 per-team subkey pipeline), and wraps it in a
/// <see cref="DefaultBoundEd25519Signer"/>.
///
/// Throws <see cref="InvalidOperationException"/> when no team is active —
/// callers must call <see cref="IActiveTeamAccessor.SetActiveAsync"/> first.
/// </summary>
public sealed class DefaultSessionSignerAccessor : ISessionSignerAccessor
{
    private readonly IRootSeedProvider _rootSeed;
    private readonly ITeamSubkeyDerivation _subkeyDerivation;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly IEd25519Signer _ed25519;

    public DefaultSessionSignerAccessor(
        IRootSeedProvider rootSeed,
        ITeamSubkeyDerivation subkeyDerivation,
        IActiveTeamAccessor activeTeam,
        IEd25519Signer ed25519)
    {
        _rootSeed         = rootSeed         ?? throw new ArgumentNullException(nameof(rootSeed));
        _subkeyDerivation = subkeyDerivation ?? throw new ArgumentNullException(nameof(subkeyDerivation));
        _activeTeam       = activeTeam       ?? throw new ArgumentNullException(nameof(activeTeam));
        _ed25519          = ed25519          ?? throw new ArgumentNullException(nameof(ed25519));
    }

    /// <inheritdoc />
    public async ValueTask<IBoundEd25519Signer> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var active = _activeTeam.Active
            ?? throw new InvalidOperationException(
                "ISessionSignerAccessor.GetCurrentAsync: no active team. "
                + "Call IActiveTeamAccessor.SetActiveAsync before requesting a session signer.");

        var rootSeed = await _rootSeed.GetRootSeedAsync(cancellationToken).ConfigureAwait(false);
        var (publicKey, privateKey) = _subkeyDerivation.DeriveTeamKeypair(
            rootSeed.Span,
            active.TeamId.ToString());
        return new DefaultBoundEd25519Signer(_ed25519, privateKey, publicKey);
    }
}
