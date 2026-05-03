using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>
/// Wave 6.3.E unit tests for <see cref="TeamStoreActivator"/>. The activator's
/// job is a narrow three-step pipeline: (1) derive each team's 32-byte
/// SQLCipher key via <see cref="ISqlCipherKeyDerivation"/>, (2) resolve the
/// team's <see cref="IEncryptedStore"/> from its <see cref="TeamContext"/>,
/// (3) call <c>OpenAsync</c>. These tests pin each step and the dedupe
/// contract.
/// </summary>
public sealed class TeamStoreActivatorTests
{
    /// <summary>The activator derives the key via ISqlCipherKeyDerivation and
    /// passes exactly that key — plus the EncryptionOptions-configured path
    /// — into IEncryptedStore.OpenAsync.</summary>
    [Fact]
    public async Task Activates_store_with_derived_key()
    {
        var seed = MakeSeed(0x11);
        var team = TeamId.New();
        var derivation = new SqlCipherKeyDerivation();
        var expectedKey = derivation.DeriveSqlCipherKey(seed, team.Value.ToString("D"));

        var recordingStore = new RecordingEncryptedStore();
        var factory = new StubTeamContextFactory();
        var expectedPath = $"/tmp/sunfish-activator-test/{team.Value:D}/sunfish.db";
        factory.AddTeam(team, recordingStore, expectedPath);

        var activator = new TeamStoreActivator(factory, derivation, seed);

        await activator.ActivateAsync(team, CancellationToken.None);

        Assert.Equal(1, recordingStore.OpenCallCount);
        Assert.Equal(expectedPath, recordingStore.LastPath);
        Assert.Equal(expectedKey, recordingStore.LastKey);
    }

    /// <summary>Second activation for the same team is a no-op — OpenAsync
    /// must be invoked exactly once across both calls.</summary>
    [Fact]
    public async Task Is_idempotent()
    {
        var seed = MakeSeed(0x22);
        var team = TeamId.New();
        var store = new RecordingEncryptedStore();
        var factory = new StubTeamContextFactory();
        factory.AddTeam(team, store, "/tmp/a.db");

        var activator = new TeamStoreActivator(factory, new SqlCipherKeyDerivation(), seed);

        await activator.ActivateAsync(team, CancellationToken.None);
        await activator.ActivateAsync(team, CancellationToken.None);
        await activator.ActivateAsync(team, CancellationToken.None);

        Assert.Equal(1, store.OpenCallCount);
    }

    /// <summary>Different teams derive different keys — the activator must
    /// pass each team's own HKDF-derived key into its store.</summary>
    [Fact]
    public async Task Different_teams_get_different_keys()
    {
        var seed = MakeSeed(0x33);
        var derivation = new SqlCipherKeyDerivation();
        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var storeA = new RecordingEncryptedStore();
        var storeB = new RecordingEncryptedStore();
        var factory = new StubTeamContextFactory();
        factory.AddTeam(teamA, storeA, "/tmp/a.db");
        factory.AddTeam(teamB, storeB, "/tmp/b.db");

        var activator = new TeamStoreActivator(factory, derivation, seed);

        await activator.ActivateAsync(teamA, CancellationToken.None);
        await activator.ActivateAsync(teamB, CancellationToken.None);

        Assert.NotNull(storeA.LastKey);
        Assert.NotNull(storeB.LastKey);
        Assert.NotEqual(storeA.LastKey, storeB.LastKey);
        Assert.Equal(
            derivation.DeriveSqlCipherKey(seed, teamA.Value.ToString("D")),
            storeA.LastKey);
        Assert.Equal(
            derivation.DeriveSqlCipherKey(seed, teamB.Value.ToString("D")),
            storeB.LastKey);
    }

    /// <summary>Concurrent activations for the same team coalesce onto a
    /// single OpenAsync invocation — the activator deduplicates in-flight
    /// work so a burst of 10 parallel callers triggers 1 open.</summary>
    [Fact]
    public async Task Concurrent_activations_for_same_team_dedupe()
    {
        var seed = MakeSeed(0x44);
        var team = TeamId.New();
        var store = new SlowEncryptedStore(openDelayMs: 20);
        var factory = new StubTeamContextFactory();
        factory.AddTeam(team, store, "/tmp/x.db");

        var activator = new TeamStoreActivator(factory, new SqlCipherKeyDerivation(), seed);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => activator.ActivateAsync(team, CancellationToken.None).AsTask())
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1, store.OpenCallCount);
    }

    /// <summary>Activating a team that was never materialized throws a clear
    /// diagnostic exception instead of NullReferenceException.</summary>
    [Fact]
    public async Task Throws_when_team_context_not_materialized()
    {
        var activator = new TeamStoreActivator(
            new StubTeamContextFactory(),
            new SqlCipherKeyDerivation(),
            MakeSeed(0x55));

        var team = TeamId.New();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => activator.ActivateAsync(team, CancellationToken.None).AsTask());
        Assert.Contains(team.Value.ToString("D"), ex.Message, StringComparison.Ordinal);
    }

    // ---- Helpers --------------------------------------------------------

    private static byte[] MakeSeed(byte fillByte)
    {
        var seed = new byte[32];
        Array.Fill(seed, fillByte);
        return seed;
    }

    /// <summary>Fake <see cref="ITeamContextFactory"/> that hands out
    /// pre-built <see cref="TeamContext"/>s whose service providers contain a
    /// test-controlled <see cref="IEncryptedStore"/> + configured
    /// <see cref="EncryptionOptions"/>.</summary>
    private sealed class StubTeamContextFactory : ITeamContextFactory
    {
        private readonly Dictionary<TeamId, TeamContext> _contexts = new();

        public void AddTeam(TeamId teamId, IEncryptedStore store, string databasePath)
        {
            var services = new ServiceCollection();
            services.AddSingleton(store);
            services.Configure<EncryptionOptions>(o => o.DatabasePath = databasePath);
            var provider = services.BuildServiceProvider();
            _contexts[teamId] = new TeamContext(teamId, $"Team {teamId.Value:N}", provider);
        }

        public IReadOnlyCollection<TeamContext> Active => _contexts.Values.ToArray();

        public Task<TeamContext> GetOrCreateAsync(TeamId teamId, string displayName, CancellationToken ct)
            => Task.FromResult(_contexts[teamId]);

        public Task RemoveAsync(TeamId teamId, CancellationToken ct)
        {
            _contexts.Remove(teamId);
            return Task.CompletedTask;
        }
    }

    /// <summary>Minimal recording <see cref="IEncryptedStore"/> — snapshots
    /// the arguments to <c>OpenAsync</c> without any real persistence.</summary>
    private class RecordingEncryptedStore : IEncryptedStore
    {
        public int OpenCallCount { get; private set; }
        public string? LastPath { get; private set; }
        public byte[]? LastKey { get; private set; }

        public virtual Task OpenAsync(string databasePath, ReadOnlyMemory<byte> key, CancellationToken ct)
        {
            OpenCallCount++;
            LastPath = databasePath;
            LastKey = key.ToArray();
            return Task.CompletedTask;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task CloseAsync() => Task.CompletedTask;
    }

    /// <summary>Recording store with an artificial delay so the dedupe test
    /// can reliably stack up concurrent awaiters while the first open is
    /// still pending.</summary>
    private sealed class SlowEncryptedStore : RecordingEncryptedStore
    {
        private readonly int _openDelayMs;
        public SlowEncryptedStore(int openDelayMs) => _openDelayMs = openDelayMs;

        public override async Task OpenAsync(string databasePath, ReadOnlyMemory<byte> key, CancellationToken ct)
        {
            // Delay BEFORE recording so concurrent callers really do pile up
            // on the in-flight task.
            await Task.Delay(_openDelayMs, ct).ConfigureAwait(false);
            await base.OpenAsync(databasePath, key, ct).ConfigureAwait(false);
        }
    }
}
