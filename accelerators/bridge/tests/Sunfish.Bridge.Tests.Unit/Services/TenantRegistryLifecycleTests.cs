using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Services;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Services;

/// <summary>
/// Transition-matrix coverage for the Wave 5.2.B lifecycle additions on
/// <see cref="TenantRegistry"/> — <c>SuspendAsync</c> / <c>ResumeAsync</c> /
/// <c>CancelAsync</c>, plus event-bus publication semantics for every
/// mutation (including <c>CreateAsync</c> and <c>SetTeamPublicKeyAsync</c>
/// which were extended with publish calls in 5.2.B).
/// </summary>
/// <remarks>
/// <para>
/// Uses the EF Core in-memory provider with a shared
/// <see cref="InMemoryDatabaseRoot"/>, matching the Wave 5.1
/// <c>TenantRegistryTests</c> harness. The provider is "in-memory" in the
/// EF sense — no SQLite. That is deliberate: the Wave 5.1 harness uses the
/// same shape and these tests extend that corpus rather than diverge.
/// </para>
/// <para>
/// Transition-matrix (11 pairings) per
/// <c>_shared/product/wave-5.2-decomposition.md</c> §5.2.B:
/// Pending → Suspended illegal;
/// Pending → Cancelled illegal;
/// Pending → Active legal (via <c>SetTeamPublicKeyAsync</c>, re-verified here);
/// Active → Suspended legal;
/// Active → Cancelled legal;
/// Active → Active idempotent (no-op on Resume);
/// Suspended → Active legal;
/// Suspended → Cancelled legal;
/// Suspended → Suspended idempotent;
/// Cancelled → Suspended illegal;
/// Cancelled → Active illegal.
/// </para>
/// </remarks>
public class TenantRegistryLifecycleTests
{
    private sealed class TestTenant : ITenantContext
    {
        public string TenantId => "unit-tenant";
        public string UserId => "unit-user";
        public IReadOnlyList<string> Roles { get; } = ["Admin"];
        public bool HasPermission(string permission) => true;
    }

    /// <summary>
    /// Optional captured bus so tests can assert publication. When the test
    /// doesn't care about events, <see cref="BuildProvider"/> returns the
    /// default <see cref="InMemoryTenantRegistryEventBus"/>.
    /// </summary>
    private sealed class CapturingEventBus : ITenantRegistryEventBus
    {
        private readonly List<TenantLifecycleEvent> _events = [];
        public IReadOnlyList<TenantLifecycleEvent> Events => _events;
        public void Publish(TenantLifecycleEvent @event) => _events.Add(@event);
        public IDisposable Subscribe(Action<TenantLifecycleEvent> handler)
            => throw new NotSupportedException(
                "CapturingEventBus is write-only for these tests; inspect Events instead.");
    }

    /// <summary>
    /// <see cref="SunfishBridgeDbContext"/> subclass whose
    /// <see cref="SaveChangesAsync(CancellationToken)"/> can be forced to throw
    /// — used by the "event-only-after-save-succeeds" test.
    /// </summary>
    private sealed class FailingSaveDbContext : SunfishBridgeDbContext
    {
        public FailingSaveDbContext(
            DbContextOptions<SunfishBridgeDbContext> options,
            IEnumerable<Sunfish.Foundation.Persistence.ISunfishEntityModule> modules,
            ITenantContext tenant)
            : base(options, modules, tenant)
        {
        }

        public bool FailOnNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailOnNextSave)
            {
                FailOnNextSave = false;
                throw new DbUpdateException("Forced save failure for Wave 5.2.B publish-ordering test.");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static (IServiceProvider sp, CapturingEventBus bus) BuildProvider(
        [System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var root = new InMemoryDatabaseRoot();
        var bus = new CapturingEventBus();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext, TestTenant>();
        services.AddDbContext<SunfishBridgeDbContext>(o => o.UseInMemoryDatabase(dbName, root));
        services.AddSingleton<ITenantRegistryEventBus>(bus);
        services.AddScoped<ITenantRegistry, TenantRegistry>();
        return (services.BuildServiceProvider(), bus);
    }

    /// <summary>Creates a tenant and optionally transitions it to a target status.
    /// Returns the tenant id.</summary>
    private static async Task<Guid> SeedTenantAsync(
        IServiceProvider sp, string slug, TenantStatus target)
    {
        Guid id;
        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var t = await registry.CreateAsync(slug, slug, "Team", CancellationToken.None);
            id = t.TenantId;
        }

        if (target == TenantStatus.Pending)
        {
            return id;
        }

        // Pending → Active via the founder flow.
        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.SetTeamPublicKeyAsync(id, new byte[] { 1, 2, 3 }, CancellationToken.None);
        }

        if (target == TenantStatus.Active)
        {
            return id;
        }

        if (target == TenantStatus.Suspended)
        {
            using var scope = sp.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.SuspendAsync(id, "seed reason", CancellationToken.None);
            return id;
        }

        if (target == TenantStatus.Cancelled)
        {
            using var scope = sp.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.CancelAsync(id, DeleteMode.RetainCiphertext, CancellationToken.None);
            return id;
        }

        return id;
    }

    // ---- Transition matrix --------------------------------------------------

    [Fact]
    public async Task Pending_to_Suspended_is_illegal()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "pending-suspend", TenantStatus.Pending);

        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await registry.SuspendAsync(id, "nope", CancellationToken.None));
    }

    [Fact]
    public async Task Pending_to_Cancelled_is_illegal()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "pending-cancel", TenantStatus.Pending);

        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await registry.CancelAsync(id, DeleteMode.RetainCiphertext, CancellationToken.None));
    }

    [Fact]
    public async Task Pending_to_Active_via_SetTeamPublicKey_is_legal_and_publishes_event()
    {
        var (sp, bus) = BuildProvider();
        var id = await SeedTenantAsync(sp, "pending-active", TenantStatus.Pending);

        // SeedTenantAsync Pending case does not call SetTeamPublicKeyAsync, so the
        // Create event is the only one on the bus so far.
        Assert.Single(bus.Events);
        Assert.Equal(TenantStatus.Pending, bus.Events[0].Previous);
        Assert.Equal(TenantStatus.Pending, bus.Events[0].Current);

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.SetTeamPublicKeyAsync(id, new byte[] { 9, 9, 9 }, CancellationToken.None);
        }

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var found = await registry.GetByIdAsync(id, CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(TenantStatus.Active, found!.Status);
        }

        // Create published (Pending=Pending) + first Active transition (Pending→Active).
        Assert.Equal(2, bus.Events.Count);
        Assert.Equal(TenantStatus.Pending, bus.Events[1].Previous);
        Assert.Equal(TenantStatus.Active, bus.Events[1].Current);
    }

    [Fact]
    public async Task Active_to_Suspended_is_legal()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "active-suspend", TenantStatus.Active);

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.SuspendAsync(id, "billing", CancellationToken.None);
        }

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var found = await registry.GetByIdAsync(id, CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(TenantStatus.Suspended, found!.Status);
            Assert.Equal("billing", found.SuspendedReason);
        }
    }

    [Fact]
    public async Task Active_to_Cancelled_is_legal_and_sets_CancelledAt()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "active-cancel", TenantStatus.Active);
        var before = DateTime.UtcNow.AddSeconds(-1);

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.CancelAsync(id, DeleteMode.SecureWipe, CancellationToken.None);
        }

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var found = await registry.GetByIdAsync(id, CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(TenantStatus.Cancelled, found!.Status);
            Assert.NotNull(found.CancelledAt);
            Assert.True(found.CancelledAt >= before);
        }
    }

    [Fact]
    public async Task Active_Resume_is_idempotent_no_event()
    {
        var (sp, bus) = BuildProvider();
        var id = await SeedTenantAsync(sp, "active-idem", TenantStatus.Active);
        var countBefore = bus.Events.Count;

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            // Already Active — Resume should no-op.
            await registry.ResumeAsync(id, CancellationToken.None);
        }

        Assert.Equal(countBefore, bus.Events.Count);
    }

    [Fact]
    public async Task Suspended_to_Active_is_legal_and_clears_SuspendedReason()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "suspend-active", TenantStatus.Suspended);

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.ResumeAsync(id, CancellationToken.None);
        }

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var found = await registry.GetByIdAsync(id, CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(TenantStatus.Active, found!.Status);
            Assert.Null(found.SuspendedReason);
        }
    }

    [Fact]
    public async Task Suspended_to_Cancelled_is_legal()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "suspend-cancel", TenantStatus.Suspended);

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.CancelAsync(id, DeleteMode.RetainCiphertext, CancellationToken.None);
        }

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var found = await registry.GetByIdAsync(id, CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(TenantStatus.Cancelled, found!.Status);
        }
    }

    [Fact]
    public async Task Suspended_Suspend_is_idempotent_no_event_no_reason_overwrite()
    {
        var (sp, bus) = BuildProvider();
        var id = await SeedTenantAsync(sp, "suspend-idem", TenantStatus.Suspended);
        // Seed suspend used reason="seed reason".
        var countBefore = bus.Events.Count;

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.SuspendAsync(id, "second reason", CancellationToken.None);
        }

        Assert.Equal(countBefore, bus.Events.Count);

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var found = await registry.GetByIdAsync(id, CancellationToken.None);
            Assert.NotNull(found);
            // First-suspend reason is preserved; idempotent no-op does not rewrite.
            Assert.Equal("seed reason", found!.SuspendedReason);
        }
    }

    [Fact]
    public async Task Cancelled_to_Suspended_is_illegal()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "cancel-suspend", TenantStatus.Cancelled);

        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await registry.SuspendAsync(id, "nope", CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_to_Active_is_illegal()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "cancel-active", TenantStatus.Cancelled);

        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await registry.ResumeAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_on_already_Cancelled_throws()
    {
        var (sp, _) = BuildProvider();
        var id = await SeedTenantAsync(sp, "cancel-twice", TenantStatus.Cancelled);

        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await registry.CancelAsync(id, DeleteMode.RetainCiphertext, CancellationToken.None));
    }

    // ---- Event-publication assertions --------------------------------------

    [Fact]
    public async Task CreateAsync_publishes_fresh_create_event_with_Previous_equals_Current_Pending()
    {
        var (sp, bus) = BuildProvider();

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.CreateAsync("create-evt", "Create Evt", "Team", CancellationToken.None);
        }

        Assert.Single(bus.Events);
        var evt = bus.Events[0];
        Assert.Equal(TenantStatus.Pending, evt.Previous);
        Assert.Equal(TenantStatus.Pending, evt.Current);
        Assert.Null(evt.Reason);
    }

    [Fact]
    public async Task SuspendAsync_publishes_TenantLifecycleEvent()
    {
        var (sp, bus) = BuildProvider();
        var id = await SeedTenantAsync(sp, "suspend-evt", TenantStatus.Active);
        var countBefore = bus.Events.Count;

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.SuspendAsync(id, "because-billing", CancellationToken.None);
        }

        Assert.Equal(countBefore + 1, bus.Events.Count);
        var evt = bus.Events[^1];
        Assert.Equal(id, evt.TenantId);
        Assert.Equal(TenantStatus.Active, evt.Previous);
        Assert.Equal(TenantStatus.Suspended, evt.Current);
        Assert.Equal("because-billing", evt.Reason);
    }

    [Fact]
    public async Task ResumeAsync_publishes_TenantLifecycleEvent()
    {
        var (sp, bus) = BuildProvider();
        var id = await SeedTenantAsync(sp, "resume-evt", TenantStatus.Suspended);
        var countBefore = bus.Events.Count;

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.ResumeAsync(id, CancellationToken.None);
        }

        Assert.Equal(countBefore + 1, bus.Events.Count);
        var evt = bus.Events[^1];
        Assert.Equal(id, evt.TenantId);
        Assert.Equal(TenantStatus.Suspended, evt.Previous);
        Assert.Equal(TenantStatus.Active, evt.Current);
        Assert.Null(evt.Reason);
    }

    [Fact]
    public async Task CancelAsync_publishes_event_with_delete_mode_reason()
    {
        var (sp, bus) = BuildProvider();
        var id = await SeedTenantAsync(sp, "cancel-evt", TenantStatus.Active);
        var countBefore = bus.Events.Count;

        using (var scope = sp.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            await registry.CancelAsync(id, DeleteMode.SecureWipe, CancellationToken.None);
        }

        Assert.Equal(countBefore + 1, bus.Events.Count);
        var evt = bus.Events[^1];
        Assert.Equal(id, evt.TenantId);
        Assert.Equal(TenantStatus.Active, evt.Previous);
        Assert.Equal(TenantStatus.Cancelled, evt.Current);
        Assert.Equal(nameof(DeleteMode.SecureWipe), evt.Reason);
    }

    [Fact]
    public async Task Event_is_only_published_after_SaveChangesAsync_succeeds()
    {
        // This test uses a manually-wired FailingSaveDbContext so SaveChangesAsync
        // can be forced to throw mid-transition. AddDbContext<TDerived>() registers
        // DbContextOptions<TDerived>, but SunfishBridgeDbContext's ctor takes the
        // BASE DbContextOptions<SunfishBridgeDbContext> — hence the options instance
        // is built by hand here and the context is constructed directly per scope.
        var root = new InMemoryDatabaseRoot();
        var bus = new CapturingEventBus();
        var tenant = new TestTenant();
        var dbName = nameof(Event_is_only_published_after_SaveChangesAsync_succeeds);
        var options = new DbContextOptionsBuilder<SunfishBridgeDbContext>()
            .UseInMemoryDatabase(dbName, root)
            .Options;
        var modules = Array.Empty<Sunfish.Foundation.Persistence.ISunfishEntityModule>();

        FailingSaveDbContext CreateDb() => new(options, modules, tenant);

        // Seed a tenant in Active state without the failure flag set.
        Guid id;
        {
            using var db = CreateDb();
            var registry = new TenantRegistry(db, bus);
            var t = await registry.CreateAsync("fail-save", "Fail Save", "Team", CancellationToken.None);
            id = t.TenantId;
            await registry.SetTeamPublicKeyAsync(id, new byte[] { 1, 2, 3 }, CancellationToken.None);
        }

        var countBefore = bus.Events.Count;

        // Now flip the failure flag and attempt to Suspend; SaveChangesAsync will
        // throw. The registry must propagate the throw and must NOT publish the
        // lifecycle event, since the DB row never committed.
        {
            using var db = CreateDb();
            db.FailOnNextSave = true;
            var registry = new TenantRegistry(db, bus);
            await Assert.ThrowsAsync<DbUpdateException>(
                async () => await registry.SuspendAsync(id, "billing", CancellationToken.None));
        }

        Assert.Equal(countBefore, bus.Events.Count);
    }
}
