using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.IdentityAtlas;
using Sunfish.Foundation.UI;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.UICore.Wayfinder;

namespace Sunfish.Anchor.Tests;

public class AnchorIdentityAtlasSurfaceTests
{
    private readonly IKeyStore _keyStore = Substitute.For<IKeyStore>();
    private readonly ITrusteeRegistry _trusteeRegistry = Substitute.For<ITrusteeRegistry>();
    private readonly IActiveTeamAccessor _activeTeam = Substitute.For<IActiveTeamAccessor>();
    private readonly ITeamRegistry _teamRegistry = Substitute.For<ITeamRegistry>();
    private readonly TenantId _tenant = new("acme");
    private readonly ActorId _actor = new("alice");

    private Services.AnchorIdentityAtlasSurface CreateSut() =>
        new(_keyStore, _trusteeRegistry, _activeTeam, _teamRegistry);

    [Fact]
    public async Task GetProfileEditAsync_ReturnsMappedViewModel()
    {
        var profile = new IdentityProfile(_actor, "Alice Smith", "alice@example.com", "+1-555-0100");
        _keyStore.GetIdentityProfileAsync(_tenant, _actor).Returns(profile);

        var result = await CreateSut().GetProfileEditAsync(_tenant, _actor);

        Assert.Equal(_actor, result.Actor);
        Assert.Equal("Alice Smith", result.DisplayName);
        Assert.Equal("alice@example.com", result.ContactEmail);
        Assert.Equal("+1-555-0100", result.PhoneNumber);
    }

    [Fact]
    public async Task GetProfileEditAsync_NullProfile_ReturnsEmptyStrings()
    {
        _keyStore.GetIdentityProfileAsync(_tenant, _actor).Returns((IdentityProfile?)null);

        var result = await CreateSut().GetProfileEditAsync(_tenant, _actor);

        Assert.Equal(string.Empty, result.DisplayName);
        Assert.Equal(string.Empty, result.ContactEmail);
        Assert.Null(result.PhoneNumber);
    }

    [Fact]
    public async Task GetKeyRotationAsync_ReturnsFingerprintAndRotationState()
    {
        var publicKey = new byte[32];
        new Random(42).NextBytes(publicKey);
        var expiry = DateTimeOffset.UtcNow.AddHours(24);
        var keyInfo = new KeyInfo(publicKey, HistoricalKeyCount: 3, RotationInProgress: true, expiry);
        _keyStore.GetCurrentKeyInfoAsync(_tenant, _actor).Returns(keyInfo);

        var result = await CreateSut().GetKeyRotationAsync(_tenant, _actor);

        Assert.Equal(_actor, result.Actor);
        Assert.Equal(KeyFingerprint.FromPublicKey(publicKey), result.CurrentFingerprint);
        Assert.Equal(3, result.HistoricalKeyCount);
        Assert.True(result.RotationInProgress);
        Assert.Equal(expiry, result.RotationWindowExpiry);
    }

    [Fact]
    public async Task GetKeyRotationAsync_NullKeyInfo_ReturnsDefaultViewModel()
    {
        _keyStore.GetCurrentKeyInfoAsync(_tenant, _actor).Returns((KeyInfo?)null);

        var result = await CreateSut().GetKeyRotationAsync(_tenant, _actor);

        Assert.Equal(_actor, result.Actor);
        Assert.Equal(default(KeyFingerprint), result.CurrentFingerprint);
        Assert.Equal(0, result.HistoricalKeyCount);
        Assert.False(result.RotationInProgress);
        Assert.Null(result.RotationWindowExpiry);
    }

    [Fact]
    public async Task GetRecoveryContactsAsync_MapsAllTrusteesAndVerificationStates()
    {
        var policy = new TrusteePolicy(MaxTrustees: 3);
        var now = DateTimeOffset.UtcNow;
        var trustees = new List<Trustee>
        {
            new(new ActorId("bob"),   "Bob Jones",   TrusteeVerificationState.Verified, now),
            new(new ActorId("carol"), "Carol White", TrusteeVerificationState.Pending,  now),
            new(new ActorId("dave"),  "Dave Black",  TrusteeVerificationState.Revoked,  now),
        };
        _trusteeRegistry.GetPolicyAsync(_tenant).Returns(policy);
        _trusteeRegistry.GetTrusteesAsync(_tenant, _actor).Returns(trustees);

        var result = await CreateSut().GetRecoveryContactsAsync(_tenant, _actor);

        Assert.Equal(3, result.Contacts.Count);
        Assert.Equal(3, result.MaxContacts);
        Assert.Equal(SyncState.Healthy,    result.Contacts[0].VerificationStatus);
        Assert.Equal(SyncState.Stale,      result.Contacts[1].VerificationStatus);
        Assert.Equal(SyncState.Quarantine, result.Contacts[2].VerificationStatus);
    }

    [Fact]
    public async Task GetHistoricalKeysAsync_ReturnsEmptyListWhileH4Pending()
    {
        var result = await CreateSut().GetHistoricalKeysAsync(_tenant, _actor);

        Assert.Equal(_actor, result.Actor);
        Assert.Empty(result.Keys);
    }

    [Fact]
    public async Task GetActiveTeamOverviewAsync_MapsAllMembershipsAndActiveTeam()
    {
        var teamGuid = Guid.NewGuid();
        var publicKey = new byte[32];
        new Random(7).NextBytes(publicKey);
        var fp = KeyFingerprint.FromPublicKey(publicKey);
        var memberships = new List<TeamMembership>
        {
            new(teamGuid, "Alpha Team", "Member", fp),
        };
        _teamRegistry.GetMembershipsAsync(_actor).Returns(memberships);

        var teamContext = new TeamContext(
            new Sunfish.Kernel.Runtime.Teams.TeamId(teamGuid),
            "Alpha Team",
            NSubstitute.Substitute.For<IServiceProvider>());
        _activeTeam.Active.Returns(teamContext);

        var result = await CreateSut().GetActiveTeamOverviewAsync(_tenant, _actor);

        Assert.Equal(_actor, result.Actor);
        Assert.Single(result.Teams);
        Assert.Equal(teamGuid, result.Teams[0].TeamId);
        Assert.Equal("Alpha Team", result.Teams[0].DisplayName);
        Assert.Equal(teamGuid, result.ActiveTeamId);

        await teamContext.DisposeAsync();
    }

    [Fact]
    public void AnchorIdentityAtlasSurface_DoesNotDependOnIFieldDecryptor()
    {
        var ctorParams = typeof(Services.AnchorIdentityAtlasSurface)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(
            ctorParams,
            t => t.FullName?.Contains("IFieldDecryptor") == true);
    }
}
