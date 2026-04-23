using System.Runtime.CompilerServices;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Notifications;

/// <summary>
/// Null-object <see cref="ITeamNotificationStream"/> that yields nothing and
/// idles until cancellation. Wave 6.3 replaces instances of this with real
/// gossip / event-log backed streams; using the empty stream keeps downstream
/// composition valid for teams whose sync transport isn't active yet.
/// </summary>
public sealed class EmptyTeamNotificationStream : ITeamNotificationStream
{
    /// <summary>Construct an empty stream bound to <paramref name="teamId"/>.</summary>
    public EmptyTeamNotificationStream(TeamId teamId) => TeamId = teamId;

    /// <inheritdoc />
    public TeamId TeamId { get; }

    /// <inheritdoc />
    public async IAsyncEnumerable<TeamNotification> Subscribe(
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Park until cancellation. Yield-break without throwing so the pump
        // observes a clean completion.
        try
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        yield break;
    }
}
