using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Ship.Common.Tests;

/// <summary>
/// W#46 P1b — coverage for
/// <see cref="DefaultPermissionResolver"/>'s subscribe-before-load
/// cache invalidation per
/// <c>shared-design-system-permres-cache-invalidation-addendum.md</c>.
/// </summary>
public class DefaultPermissionResolverCacheInvalidationTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly DateTimeOffset T0 = new(2026, 5, 6, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Test-local in-process IStandingOrderEventStream — the canonical
    /// <c>InMemoryStandingOrderEventStream</c> is internal to foundation-wayfinder
    /// (W#57 council amendment) so this assembly cannot reach it; the test stream
    /// implements only what these tests need.</summary>
    private sealed class TestEventStream : IStandingOrderEventStream
    {
        private readonly List<Action<StandingOrderAppliedEvent>> _subs = new();
        public IReadOnlyList<StandingOrderAppliedEvent> ReplayAll() =>
            Array.Empty<StandingOrderAppliedEvent>();
        public IDisposable Subscribe(Action<StandingOrderAppliedEvent> handler)
        {
            _subs.Add(handler);
            return new Subscription(this, handler);
        }
        public void Publish(StandingOrderAppliedEvent e)
        {
            // Snapshot under no lock — single-threaded test usage.
            var snap = _subs.ToArray();
            foreach (var h in snap) h(e);
        }
        private sealed class Subscription : IDisposable
        {
            private readonly TestEventStream _owner;
            private readonly Action<StandingOrderAppliedEvent> _handler;
            private bool _disposed;
            public Subscription(TestEventStream o, Action<StandingOrderAppliedEvent> h)
            { _owner = o; _handler = h; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner._subs.Remove(_handler);
            }
        }
    }

    private static PrincipalId NewPrincipalId()
    {
        var bytes = new byte[32];
        Random.Shared.NextBytes(bytes);
        return PrincipalId.FromBytes(bytes);
    }

    private static IOperationSigner NewSigner()
    {
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(PrincipalId.FromBytes(new byte[32]));
        return signer;
    }

    private static IShipRoleAssignmentSource SourceWith(
        TenantId forTenant, ActorId holder)
    {
        var source = Substitute.For<IShipRoleAssignmentSource>();
        var assignments = new[]
        {
            new ShipRoleAssignment(
                forTenant, holder, ShipRole.Captain,
                Division: null, AssignedAt: T0, RotatesAt: null,
                IssuedBy: new StandingOrderId(Guid.NewGuid())),
        };
        source.LoadAssignmentsAsync(forTenant, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ShipRoleAssignment>>(assignments));
        // Other tenants: empty assignments → resolves return Denied without
        // throwing (tests pin invalidation, not authority outcomes).
        source.LoadAssignmentsAsync(
                Arg.Is<TenantId>(t => !t.Equals(forTenant)),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ShipRoleAssignment>>(
                Array.Empty<ShipRoleAssignment>()));
        return source;
    }

    private static StandingOrderAppliedEvent NewAppliedEvent(
        TenantId tenantId, StandingOrderScope scope) =>
        new(
            StandingOrderId: new StandingOrderId(Guid.NewGuid()),
            TenantId: tenantId,
            IssuedBy: new ActorId("system"),
            AppliedAt: T0,
            Scope: scope,
            Triples: Array.Empty<StandingOrderTriple>(),
            AuditRecordId: new AuditRecordId(Guid.NewGuid()),
            Rationale: null);

    [Fact]
    public async Task Cache_InvalidatedOnStandingOrderApplied_TenantScope()
    {
        var pid = NewPrincipalId();
        var holder = new ActorId(pid.ToBase64Url());
        var source = SourceWith(TenantA, holder);
        var stream = new TestEventStream();

        using var resolver = new DefaultPermissionResolver(
            source, Substitute.For<IAuditTrail>(), NewSigner(),
            NullLogger<DefaultPermissionResolver>.Instance,
            eventStream: stream);

        var subject = new Individual(pid);

        await resolver.ResolveAsync(TenantA, subject,
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);
        await source.Received(1).LoadAssignmentsAsync(
            TenantA, Arg.Any<CancellationToken>());

        stream.Publish(NewAppliedEvent(TenantA, StandingOrderScope.Tenant));

        await resolver.ResolveAsync(TenantA, subject,
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);
        await source.Received(2).LoadAssignmentsAsync(
            TenantA, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_InvalidatedOnStandingOrderApplied_PlatformScope()
    {
        var pidA = NewPrincipalId();
        var holderA = new ActorId(pidA.ToBase64Url());
        var pidB = NewPrincipalId();
        var holderB = new ActorId(pidB.ToBase64Url());

        var source = Substitute.For<IShipRoleAssignmentSource>();
        source.LoadAssignmentsAsync(TenantA, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ShipRoleAssignment>>(new[]
            {
                new ShipRoleAssignment(TenantA, holderA, ShipRole.Captain,
                    Division: null, T0, null,
                    IssuedBy: new StandingOrderId(Guid.NewGuid())),
            }));
        source.LoadAssignmentsAsync(TenantB, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ShipRoleAssignment>>(new[]
            {
                new ShipRoleAssignment(TenantB, holderB, ShipRole.Captain,
                    Division: null, T0, null,
                    IssuedBy: new StandingOrderId(Guid.NewGuid())),
            }));

        var stream = new TestEventStream();
        using var resolver = new DefaultPermissionResolver(
            source, Substitute.For<IAuditTrail>(), NewSigner(),
            NullLogger<DefaultPermissionResolver>.Instance,
            eventStream: stream);

        await resolver.ResolveAsync(TenantA, new Individual(pidA),
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);
        await resolver.ResolveAsync(TenantB, new Individual(pidB),
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);

        await source.Received(1).LoadAssignmentsAsync(TenantA, Arg.Any<CancellationToken>());
        await source.Received(1).LoadAssignmentsAsync(TenantB, Arg.Any<CancellationToken>());

        stream.Publish(NewAppliedEvent(TenantA, StandingOrderScope.Platform));

        await resolver.ResolveAsync(TenantA, new Individual(pidA),
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);
        await resolver.ResolveAsync(TenantB, new Individual(pidB),
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);

        await source.Received(2).LoadAssignmentsAsync(TenantA, Arg.Any<CancellationToken>());
        await source.Received(2).LoadAssignmentsAsync(TenantB, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_NotInvalidatedOnStandingOrderApplied_IntegrationScope()
    {
        var pid = NewPrincipalId();
        var holder = new ActorId(pid.ToBase64Url());
        var source = SourceWith(TenantA, holder);
        var stream = new TestEventStream();

        using var resolver = new DefaultPermissionResolver(
            source, Substitute.For<IAuditTrail>(), NewSigner(),
            NullLogger<DefaultPermissionResolver>.Instance,
            eventStream: stream);

        var subject = new Individual(pid);

        await resolver.ResolveAsync(TenantA, subject,
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);

        // Integration-scoped event — no-op for permission cache per
        // addendum scope-reasoning.
        stream.Publish(NewAppliedEvent(TenantA, StandingOrderScope.Integration));

        // Second resolve cache-served — source called only once.
        await resolver.ResolveAsync(TenantA, subject,
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);

        await source.Received(1).LoadAssignmentsAsync(
            TenantA, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var stream = new TestEventStream();
        var resolver = new DefaultPermissionResolver(
            Substitute.For<IShipRoleAssignmentSource>(),
            Substitute.For<IAuditTrail>(), NewSigner(),
            NullLogger<DefaultPermissionResolver>.Instance,
            eventStream: stream);

        resolver.Dispose();
        resolver.Dispose(); // must not throw — _disposed guard short-circuits.
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromEventStream()
    {
        var pid = NewPrincipalId();
        var holder = new ActorId(pid.ToBase64Url());
        var source = SourceWith(TenantA, holder);
        var stream = new TestEventStream();

        var resolver = new DefaultPermissionResolver(
            source, Substitute.For<IAuditTrail>(), NewSigner(),
            NullLogger<DefaultPermissionResolver>.Instance,
            eventStream: stream);

        var subject = new Individual(pid);

        await resolver.ResolveAsync(TenantA, subject,
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);

        // Dispose unsubscribes; subsequent events MUST NOT invalidate.
        resolver.Dispose();

        stream.Publish(NewAppliedEvent(TenantA, StandingOrderScope.Tenant));
        stream.Publish(NewAppliedEvent(TenantA, StandingOrderScope.Platform));

        // Second resolve still cache-served — source called only once total.
        await resolver.ResolveAsync(TenantA, subject,
            ShipLocation.Quarterdeck, DeckDepth.TopDeck, ShipAction.Read, null);

        await source.Received(1).LoadAssignmentsAsync(
            TenantA, Arg.Any<CancellationToken>());
    }
}
