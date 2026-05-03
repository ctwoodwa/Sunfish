using System.Collections.Concurrent;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Orchestration;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Orchestration;

/// <summary>
/// Unit tests for Wave 5.2.C.1 <see cref="TenantLifecycleCoordinator"/>. Uses
/// a fake <see cref="ITenantProcessSupervisor"/> + the real
/// <see cref="InMemoryTenantRegistryEventBus"/> so the dispatch mapping is
/// verified end-to-end without spawning OS processes.
/// </summary>
public sealed class TenantLifecycleCoordinatorTests
{
    private static readonly Guid Tenant = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task Publishes_Pending_to_Active_triggers_StartAsync()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var fake = new RecordingSupervisor();
        var coordinator = new TenantLifecycleCoordinator(bus, fake);
        await coordinator.StartAsync(CancellationToken.None);

        bus.Publish(new TenantLifecycleEvent(
            Tenant, TenantStatus.Pending, TenantStatus.Active, DateTimeOffset.UtcNow, null));

        await fake.WaitForCallAsync("Start", 1);
        Assert.Contains(("Start", Tenant, (DeleteMode?)null), fake.Calls);

        await coordinator.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Active_to_Suspended_triggers_PauseAsync()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var fake = new RecordingSupervisor();
        var coordinator = new TenantLifecycleCoordinator(bus, fake);
        await coordinator.StartAsync(CancellationToken.None);

        bus.Publish(new TenantLifecycleEvent(
            Tenant, TenantStatus.Active, TenantStatus.Suspended, DateTimeOffset.UtcNow, "billing"));

        await fake.WaitForCallAsync("Pause", 1);
        Assert.Contains(("Pause", Tenant, (DeleteMode?)null), fake.Calls);

        await coordinator.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Suspended_to_Active_triggers_ResumeAsync()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var fake = new RecordingSupervisor();
        var coordinator = new TenantLifecycleCoordinator(bus, fake);
        await coordinator.StartAsync(CancellationToken.None);

        bus.Publish(new TenantLifecycleEvent(
            Tenant, TenantStatus.Suspended, TenantStatus.Active, DateTimeOffset.UtcNow, null));

        await fake.WaitForCallAsync("Resume", 1);
        Assert.Contains(("Resume", Tenant, (DeleteMode?)null), fake.Calls);

        await coordinator.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Any_to_Cancelled_triggers_StopAndEraseAsync()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var fake = new RecordingSupervisor();
        var coordinator = new TenantLifecycleCoordinator(bus, fake);
        await coordinator.StartAsync(CancellationToken.None);

        // Reason carries "SecureWipe" — coordinator should parse it.
        bus.Publish(new TenantLifecycleEvent(
            Tenant, TenantStatus.Active, TenantStatus.Cancelled,
            DateTimeOffset.UtcNow, "SecureWipe"));

        await fake.WaitForCallAsync("StopAndErase", 1);
        Assert.Contains(("StopAndErase", Tenant, (DeleteMode?)DeleteMode.SecureWipe), fake.Calls);

        await coordinator.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Any_to_Cancelled_with_missing_reason_defaults_to_RetainCiphertext()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var fake = new RecordingSupervisor();
        var coordinator = new TenantLifecycleCoordinator(bus, fake);
        await coordinator.StartAsync(CancellationToken.None);

        bus.Publish(new TenantLifecycleEvent(
            Tenant, TenantStatus.Active, TenantStatus.Cancelled,
            DateTimeOffset.UtcNow, null));

        await fake.WaitForCallAsync("StopAndErase", 1);
        Assert.Contains(("StopAndErase", Tenant, (DeleteMode?)DeleteMode.RetainCiphertext), fake.Calls);

        await coordinator.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Idempotent_events_are_forwarded_as_noops()
    {
        // Pending → Pending is the fresh-create marker — coordinator does NOT
        // dispatch anything in response, matching the plan (spawn at Active,
        // not at signup).
        var bus = new InMemoryTenantRegistryEventBus();
        var fake = new RecordingSupervisor();
        var coordinator = new TenantLifecycleCoordinator(bus, fake);
        await coordinator.StartAsync(CancellationToken.None);

        bus.Publish(new TenantLifecycleEvent(
            Tenant, TenantStatus.Pending, TenantStatus.Pending,
            DateTimeOffset.UtcNow, null));

        // Tiny wait to give the background dispatch task a chance to land
        // (we assert there's no dispatch, not that one was observed).
        await Task.Delay(100);

        Assert.Empty(fake.Calls);
        await coordinator.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Minimal recording implementation that tracks every call and offers a
    /// <see cref="WaitForCallAsync"/> helper so tests don't busy-wait.
    /// </summary>
    private sealed class RecordingSupervisor : ITenantProcessSupervisor
    {
        public ConcurrentQueue<(string Op, Guid TenantId, DeleteMode? Mode)> Calls { get; } = new();

        public event EventHandler<TenantProcessEvent>? StateChanged;

        public ValueTask StartAsync(Guid tenantId, CancellationToken ct)
        {
            Calls.Enqueue(("Start", tenantId, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask PauseAsync(Guid tenantId, CancellationToken ct)
        {
            Calls.Enqueue(("Pause", tenantId, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask ResumeAsync(Guid tenantId, CancellationToken ct)
        {
            Calls.Enqueue(("Resume", tenantId, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAndEraseAsync(Guid tenantId, DeleteMode mode, CancellationToken ct)
        {
            Calls.Enqueue(("StopAndErase", tenantId, mode));
            return ValueTask.CompletedTask;
        }

        public ValueTask<TenantProcessState> GetStateAsync(Guid tenantId, CancellationToken ct)
            => ValueTask.FromResult(TenantProcessState.Unknown);

        public async Task WaitForCallAsync(string op, int minCount, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (Calls.Count(c => c.Op == op) >= minCount)
                {
                    // Touch the event to satisfy "never-used" diagnostics
                    // when tests don't subscribe — harmless at runtime.
                    _ = StateChanged;
                    return;
                }
                await Task.Delay(20);
            }
            throw new TimeoutException(
                $"Timed out waiting for {minCount} {op} call(s); observed {Calls.Count(c => c.Op == op)}.");
        }
    }
}
