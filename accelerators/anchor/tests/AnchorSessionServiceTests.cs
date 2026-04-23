using System.ComponentModel;
using Sunfish.Anchor.Services;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;

namespace Sunfish.Anchor.Tests;

public sealed class AnchorSessionServiceTests
{
    [Fact]
    public void Default_state_is_offline_across_all_three_indicators()
    {
        var svc = new AnchorSessionService();

        Assert.Equal(SyncState.Offline, svc.NodeHealth);
        Assert.Equal(SyncState.Offline, svc.LinkStatus);
        Assert.Equal(SyncState.Offline, svc.DataFreshness);
        Assert.Null(svc.NodeId);
        Assert.Null(svc.TeamId);
        Assert.Null(svc.LastSyncedAt);
        Assert.False(svc.IsOnboarded);
    }

    [Fact]
    public async Task IsOnboarded_flips_after_OnboardAsync()
    {
        var svc = new AnchorSessionService();
        var att = MakeAttestation();

        await svc.OnboardAsync(att, ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.True(svc.IsOnboarded);
    }

    [Fact]
    public void SetState_fires_PropertyChanged_for_each_changed_indicator()
    {
        var svc = new AnchorSessionService();
        var changes = new List<string?>();
        svc.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        svc.SetState(SyncState.Healthy, SyncState.Stale, SyncState.ConflictPending);

        Assert.Contains(nameof(AnchorSessionService.NodeHealth), changes);
        Assert.Contains(nameof(AnchorSessionService.LinkStatus), changes);
        Assert.Contains(nameof(AnchorSessionService.DataFreshness), changes);
    }

    [Fact]
    public void SetState_does_not_fire_for_unchanged_values()
    {
        var svc = new AnchorSessionService();
        svc.SetState(SyncState.Healthy, SyncState.Healthy, SyncState.Healthy);
        var changes = new List<string?>();
        svc.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        // Same values — no events.
        svc.SetState(SyncState.Healthy, SyncState.Healthy, SyncState.Healthy);

        Assert.Empty(changes);
    }

    [Fact]
    public async Task Onboard_with_valid_bundle_sets_NodeId_and_TeamId()
    {
        var svc = new AnchorSessionService();
        var att = MakeAttestation();

        await svc.OnboardAsync(att, ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.Equal(Convert.ToHexString(att.SubjectPublicKey), svc.NodeId);
        Assert.Equal(Convert.ToHexString(att.TeamId), svc.TeamId);
        Assert.Same(att, svc.Attestation);
    }

    [Fact]
    public async Task Onboard_stamps_LastSyncedAt()
    {
        var svc = new AnchorSessionService();
        var before = DateTimeOffset.UtcNow;

        await svc.OnboardAsync(MakeAttestation(), ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.NotNull(svc.LastSyncedAt);
        Assert.True(svc.LastSyncedAt!.Value >= before);
    }

    [Fact]
    public async Task Onboard_transitions_NodeHealth_to_Healthy()
    {
        var svc = new AnchorSessionService();
        await svc.OnboardAsync(MakeAttestation(), ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.Equal(SyncState.Healthy, svc.NodeHealth);
        Assert.Equal(SyncState.Healthy, svc.DataFreshness);
        // LinkStatus stays Offline until we actually connect to a peer.
        Assert.Equal(SyncState.Offline, svc.LinkStatus);
    }

    [Fact]
    public async Task Reset_returns_to_un_onboarded_state()
    {
        var svc = new AnchorSessionService();
        await svc.OnboardAsync(MakeAttestation(), ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        Assert.True(svc.IsOnboarded);

        svc.Reset();

        Assert.False(svc.IsOnboarded);
        Assert.Null(svc.NodeId);
        Assert.Equal(SyncState.Offline, svc.NodeHealth);
    }

    private static RoleAttestation MakeAttestation()
    {
        var teamId = new byte[RoleAttestation.TeamIdLength];
        System.Security.Cryptography.RandomNumberGenerator.Fill(teamId);
        var subject = new byte[RoleAttestation.PublicKeyLength];
        System.Security.Cryptography.RandomNumberGenerator.Fill(subject);
        var issuer = new byte[RoleAttestation.PublicKeyLength];
        System.Security.Cryptography.RandomNumberGenerator.Fill(issuer);
        var sig = new byte[RoleAttestation.SignatureLength];
        var now = DateTimeOffset.UtcNow;
        return new RoleAttestation(
            TeamId: teamId,
            SubjectPublicKey: subject,
            Role: "team_member",
            IssuedAt: now,
            ExpiresAt: now.AddDays(30),
            IssuerPublicKey: issuer,
            Signature: sig);
    }
}
