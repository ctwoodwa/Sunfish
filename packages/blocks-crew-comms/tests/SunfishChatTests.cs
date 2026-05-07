using Bunit;
using Microsoft.AspNetCore.Components;
using Sunfish.Blocks.CrewComms;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Transport;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

/// <summary>
/// W#59 Phase 4 — SunfishChat Blazor component bUnit tests per the
/// hand-off acceptance gate (≥8 cases). Pre-merge council mandatory:
/// WCAG/a11y subagent + 4-perspective.
/// </summary>
public class SunfishChatTests : BunitContext
{
    private static readonly TenantId Tenant = new("00000000-0000-0000-0000-00000000000a");

    [Fact]
    public void RendersChatRegion_WithAccessibleLandmark()
    {
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var section = cut.Find("section[role='region']");
        Assert.Equal("Crew chat", section.GetAttribute("aria-label"));
    }

    [Fact]
    public void EmptyRoster_RendersEmptyStateMessage()
    {
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var empty = cut.Find("[data-test-id='presence-empty']");
        Assert.Contains("No crew members", empty.TextContent);
    }

    [Fact]
    public void NonEmptyRoster_RendersOneRowPerCrewMember()
    {
        var roster = new FakeRoster(new[]
        {
            new CrewMember { Peer = new PeerId("alpha"), DisplayName = "Alpha" },
            new CrewMember { Peer = new PeerId("bravo"), DisplayName = "Bravo" },
        });
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, roster)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var rows = cut.FindAll("[data-test-id='crew-row']");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void PresenceDot_HasAriaLabel_NotColorAlone_PerWcag141()
    {
        var roster = new FakeRoster(new[]
        {
            new CrewMember { Peer = new PeerId("alpha"), DisplayName = "Alpha" },
        });
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, roster)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var dot = cut.Find(".sunfish-chat-presence-dot");
        // aria-label MUST be set; presence is conveyed by colour (CSS data
        // attribute) AND by the aria-label text — never colour alone.
        Assert.False(string.IsNullOrEmpty(dot.GetAttribute("aria-label")));
        Assert.Equal("img", dot.GetAttribute("role"));
    }

    [Fact]
    public void Thread_HasRoleLog_AriaLivePolite_PerWcag413()
    {
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var thread = cut.Find("[data-test-id='thread']");
        Assert.Equal("log", thread.GetAttribute("role"));
        Assert.Equal("polite", thread.GetAttribute("aria-live"));
        Assert.Equal("false", thread.GetAttribute("aria-atomic"));
    }

    [Fact]
    public void SendButton_DisabledWhenInputEmptyOrNoActiveSession()
    {
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var button = cut.Find("[data-test-id='send-button']");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void SendInput_HasAssociatedLabel_PerWcag131()
    {
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var input = cut.Find("[data-test-id='send-input']");
        var inputId = input.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(inputId));

        var label = cut.Find($"label[for='{inputId}']");
        Assert.NotNull(label);
        Assert.False(string.IsNullOrEmpty(label.TextContent.Trim()));
    }

    [Fact]
    public void IncomingInvitation_RendersAlertdialog_WithModalAttributes()
    {
        var bus = new FakeInvitationBus();
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, bus)
            .Add(c => c.Tenant, Tenant));

        Assert.Empty(cut.FindAll("[data-test-id='invitation-prompt']"));

        bus.Push(new FakeInvitation { FromPeer = new PeerId("alpha-peer") });
        cut.WaitForState(() => cut.FindAll("[data-test-id='invitation-prompt']").Count > 0);

        var prompt = cut.Find("[data-test-id='invitation-prompt']");
        Assert.Equal("alertdialog", prompt.GetAttribute("role"));
        Assert.Equal("true", prompt.GetAttribute("aria-modal"));
        Assert.False(string.IsNullOrEmpty(prompt.GetAttribute("aria-labelledby")));
        Assert.False(string.IsNullOrEmpty(prompt.GetAttribute("aria-describedby")));

        var accept = cut.Find("[data-test-id='invitation-accept']");
        var reject = cut.Find("[data-test-id='invitation-reject']");
        Assert.NotNull(accept);
        Assert.NotNull(reject);
    }

    [Fact]
    public void InviteButton_VisibleTextOnly_NoRedundantAriaLabel_PerWcag253()
    {
        var roster = new FakeRoster(new[]
        {
            new CrewMember { Peer = new PeerId("alpha"), DisplayName = "Alpha" },
        });
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, roster)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.Tenant, Tenant));

        var inviteButton = cut.Find("[data-test-id='invite-button']");
        Assert.Contains("Invite", inviteButton.TextContent);
        // Visible text IS the accessible name (WCAG 2.5.3 Label in Name).
        // aria-label MUST NOT duplicate visible text — it'd cause SR
        // double-announcement and break the cohort M2-amendment pattern
        // applied to W#53 P2 PR 2c-blazor.
        var ariaLabel = inviteButton.GetAttribute("aria-label");
        Assert.True(string.IsNullOrEmpty(ariaLabel));
    }

    [Fact]
    public async Task SendMessage_AppendsToThread_AndClearsDraft()
    {
        // MAJOR-2 from council review: pin the send-roundtrip path so a
        // future regression in HandleSendAsync (e.g. dropping the _messages.Add
        // call) surfaces at CI time. Hand-off acceptance §3 (in-order
        // thread) + §6 (scrollback preserved) both depend on this.
        var session = new FakeActiveSession();
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, NoopObservable<IChannelInvitation>())
            .Add(c => c.ActiveSession, session)
            .Add(c => c.LocalDisplayName, "Test User")
            .Add(c => c.Tenant, Tenant));

        // Type a message into the input. The component listens on @oninput
        // (not @onchange), so use Input.
        var input = cut.Find("[data-test-id='send-input']");
        input.Input("hello");

        // Submit by clicking Send (the form's @onsubmit also handles Enter).
        var sendButton = cut.Find("[data-test-id='send-button']");
        await sendButton.ClickAsync(new());

        // Message lands in the thread.
        var rendered = cut.FindAll("[data-test-id='chat-msg']");
        Assert.Single(rendered);
        Assert.Contains("hello", rendered[0].TextContent);

        // SendTextAsync was invoked exactly once with the trimmed body.
        Assert.Equal(1, session.SendCount);
        Assert.Equal("hello", session.LastSentBody);

        // Draft input cleared post-send.
        var inputAfter = cut.Find("[data-test-id='send-input']");
        var valueAttr = inputAfter.GetAttribute("value");
        Assert.True(string.IsNullOrEmpty(valueAttr));
    }

    [Fact]
    public void NullInboundInvitations_RendersWithoutCrash()
    {
        // Defense-in-depth: hosts that haven't yet wired the bus should
        // get a degraded-but-present chat surface, not a render exception.
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, null)
            .Add(c => c.Tenant, Tenant));

        var section = cut.Find("section[role='region']");
        Assert.NotNull(section);
    }

    [Fact]
    public void DisposingComponent_UnsubscribesFromBus_NoCrash()
    {
        var bus = new FakeInvitationBus();
        var cut = Render<SunfishChat>(p => p
            .Add(c => c.Provider, FakeProvider.Default())
            .Add(c => c.Roster, FakeRoster.Empty)
            .Add(c => c.InboundInvitations, bus)
            .Add(c => c.Tenant, Tenant));

        cut.Dispose();

        // After dispose, pushing more invitations must not raise (the
        // observer is unsubscribed; the bus is responsible for not
        // delivering to disposed observers).
        bus.Push(new FakeInvitation { FromPeer = new PeerId("post-dispose") });
        Assert.Equal(0, bus.LastObserverThrewCount);
    }

    private static IObservable<T> NoopObservable<T>() => new NoopObservableImpl<T>();

    private sealed class NoopObservableImpl<T> : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer) => new NoopDisposable();
        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
    }

    private sealed class FakeProvider : IChannelProvider
    {
        public ChannelCapability Capabilities => ChannelCapability.Text;

        public Task<IReadOnlyList<CrewPresence>> GetPresentCrewAsync(TenantId tenant, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CrewPresence>>(Array.Empty<CrewPresence>());

        public Task<IChannelSession> OpenAsync(TenantId tenant, PeerId peer, ChannelCapability preferredCapabilities, CancellationToken ct)
            => throw new NotSupportedException();

        public IAsyncEnumerable<IChannelInvitation> ListenAsync(TenantId tenant, CancellationToken ct)
            => EmptyAsync<IChannelInvitation>();

        public static FakeProvider Default() => new();

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class FakeRoster : ICrewRoster
    {
        public static readonly FakeRoster Empty = new(Array.Empty<CrewMember>());

        private readonly IReadOnlyList<CrewMember> _members;

        public FakeRoster(IEnumerable<CrewMember> members)
        {
            _members = members.ToList();
        }

        public Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
            => Task.FromResult(_members);
    }

    private sealed class FakeActiveSession : IChannelSession
    {
        public PeerId Peer { get; } = new("remote-peer");
        public ChannelCapability Capability { get; } = ChannelCapability.Text;
        public ChannelSessionState State { get; private set; } = ChannelSessionState.Active;
        public Task<ChannelTerminationReason> Completed { get; } =
            new TaskCompletionSource<ChannelTerminationReason>().Task;

        public int SendCount { get; private set; }
        public string? LastSentBody { get; private set; }

        public Task SendTextAsync(string message, CancellationToken ct)
        {
            SendCount++;
            LastSentBody = message;
            return Task.CompletedTask;
        }

        public Task SendAudioFrameAsync(ReadOnlyMemory<byte> opusFrame, CancellationToken ct)
            => throw new NotSupportedException();

        public IAsyncEnumerable<string> ReceiveTextAsync(CancellationToken ct)
            => EmptyAsync<string>();

        public IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAudioFramesAsync(CancellationToken ct)
            => EmptyAsync<ReadOnlyMemory<byte>>();

        public Task CloseAsync(CancellationToken ct)
        {
            State = ChannelSessionState.Terminated;
            return Task.CompletedTask;
        }

        // W#45 P4.5 PR 2 — TYPING + DELIVERED on IChannelSession surface.
        public Task SendTypingAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SendDeliveredAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public IAsyncEnumerable<DateTimeOffset> ReceiveTypingAsync(CancellationToken ct)
            => EmptyAsync<DateTimeOffset>();
        public IAsyncEnumerable<Guid> ReceiveDeliveredAsync(CancellationToken ct)
            => EmptyAsync<Guid>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class FakeInvitation : IChannelInvitation
    {
        public PeerId FromPeer { get; init; }
        public ChannelCapability OfferedCapabilities { get; init; } = ChannelCapability.Text;

        public Task<IChannelSession> AcceptAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task RejectAsync(string? reason, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeInvitationBus : IObservable<IChannelInvitation>
    {
        private readonly List<IObserver<IChannelInvitation>> _observers = new();

        public int LastObserverThrewCount { get; private set; }

        public IDisposable Subscribe(IObserver<IChannelInvitation> observer)
        {
            lock (_observers) { _observers.Add(observer); }
            return new Subscription(this, observer);
        }

        public void Push(IChannelInvitation invitation)
        {
            IObserver<IChannelInvitation>[] snapshot;
            lock (_observers) { snapshot = _observers.ToArray(); }
            foreach (var observer in snapshot)
            {
                try { observer.OnNext(invitation); }
                catch { LastObserverThrewCount++; }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly FakeInvitationBus _bus;
            private IObserver<IChannelInvitation>? _observer;

            public Subscription(FakeInvitationBus bus, IObserver<IChannelInvitation> observer)
            {
                _bus = bus;
                _observer = observer;
            }

            public void Dispose()
            {
                var observer = Interlocked.Exchange(ref _observer, null);
                if (observer is null) return;
                lock (_bus._observers) { _bus._observers.Remove(observer); }
            }
        }
    }
}
