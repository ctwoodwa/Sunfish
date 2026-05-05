using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

public class OodWatchExpiryServiceTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly DateTimeOffset T0 = new(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SweepOnce_ExpiresCandidates_AndEmitsAuditEvent()
    {
        var repo = Substitute.For<IOodWatchRepository>();
        var sweepRepo = Substitute.For<IOodWatchSweepRepository>();
        var audit = Substitute.For<IAuditTrail>();
        var signer = DefaultOodWatchServiceTests.NewSigner();
        var time = new FakeTimeProvider(T0.AddHours(9));

        var stale = new OodWatch(
            Id: OodWatchId.NewId(),
            TenantId: Tenant,
            OnWatchActor: new ActorId("a"),
            Role: OodRole.OfficerOfTheDeck,
            StartedAt: T0,
            RelievedAt: null,
            StartedBy: new ActorId("b"),
            RelievedBy: null,
            MaxWatchDuration: TimeSpan.FromHours(8),
            State: OodWatchState.Active);
        var expired = stale with { State = OodWatchState.Expired, RelievedAt = T0.AddHours(9) };

        // R4 (XO post-merge council 2026-05-06): the cross-tenant sweep
        // enumerator now lives on IOodWatchSweepRepository. ExpireWatchAsync
        // remains on the per-tenant IOodWatchRepository.
        sweepRepo.GetExpiredCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(AsAsyncEnumerable(stale));
        repo.ExpireWatchAsync(stale.Id, Arg.Any<CancellationToken>()).Returns(expired);

        var svc = new OodWatchExpiryService(
            repo,
            sweepRepo,
            NullLogger<OodWatchExpiryService>.Instance,
            audit,
            signer,
            time,
            sweepInterval: TimeSpan.FromMilliseconds(1));

        await svc.SweepOnceAsync(CancellationToken.None);

        await repo.Received(1).ExpireWatchAsync(stale.Id, Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.OodWatchExpired),
            Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
