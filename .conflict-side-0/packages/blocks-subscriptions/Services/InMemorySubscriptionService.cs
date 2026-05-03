using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Subscriptions.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Blocks.Subscriptions.Services;

/// <summary>
/// In-memory implementation of <see cref="ISubscriptionService"/> backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> stores. Suitable for demos,
/// integration tests, and kitchen-sink scenarios. Not intended for production use —
/// no persistence, no event bus.
/// </summary>
/// <remarks>
/// The in-memory service scopes reads and writes to the current
/// <see cref="ITenantContext.TenantId"/>. If no <see cref="ITenantContext"/> is
/// supplied, the service falls back to <see cref="TenantId.Default"/>.
/// The catalog is seeded with three default <see cref="Plan"/>s (Lite, Standard, Enterprise).
/// </remarks>
public sealed class InMemorySubscriptionService : ISubscriptionService
{
    private readonly ConcurrentDictionary<SubscriptionId, Subscription> _subscriptions = new();
    private readonly ConcurrentDictionary<UsageMeterId, UsageMeter> _meters = new();
    private readonly ConcurrentDictionary<Guid, MeteredUsage> _usage = new();
    private readonly IReadOnlyList<Plan> _plans;
    private readonly ITenantContext? _tenantContext;

    /// <summary>
    /// Creates a new <see cref="InMemorySubscriptionService"/> with a seeded plan
    /// catalog and an optional tenant context. If <paramref name="tenantContext"/>
    /// is <see langword="null"/>, <see cref="TenantId.Default"/> is used for all
    /// tenant-scoped operations.
    /// </summary>
    /// <param name="tenantContext">Optional tenant context used to scope operations.</param>
    public InMemorySubscriptionService(ITenantContext? tenantContext = null)
    {
        _tenantContext = tenantContext;
        _plans =
        [
            new Plan
            {
                Id = new PlanId("plan-lite"),
                Name = "Lite",
                Edition = Edition.Lite,
                MonthlyPrice = 29m,
                Description = "Essentials for small teams."
            },
            new Plan
            {
                Id = new PlanId("plan-standard"),
                Name = "Standard",
                Edition = Edition.Standard,
                MonthlyPrice = 99m,
                Description = "Growth features for most teams."
            },
            new Plan
            {
                Id = new PlanId("plan-enterprise"),
                Name = "Enterprise",
                Edition = Edition.Enterprise,
                MonthlyPrice = 299m,
                Description = "Full feature set with priority support."
            }
        ];
    }

    private TenantId CurrentTenant =>
        _tenantContext is null ? TenantId.Default : new TenantId(_tenantContext.TenantId);

    /// <inheritdoc />
    public async IAsyncEnumerable<Plan> ListPlansAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var plan in _plans)
        {
            ct.ThrowIfCancellationRequested();
            yield return plan;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public ValueTask<Subscription?> GetSubscriptionAsync(SubscriptionId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_subscriptions.TryGetValue(id, out var sub))
            return ValueTask.FromResult<Subscription?>(null);

        return sub.TenantId == CurrentTenant
            ? ValueTask.FromResult<Subscription?>(sub)
            : ValueTask.FromResult<Subscription?>(null);
    }

    /// <inheritdoc />
    public ValueTask<Subscription> CreateSubscriptionAsync(
        CreateSubscriptionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var subscription = new Subscription
        {
            Id = SubscriptionId.NewId(),
            TenantId = CurrentTenant,
            PlanId = request.PlanId,
            Edition = request.Edition,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AddOns = []
        };

        _subscriptions[subscription.Id] = subscription;
        return ValueTask.FromResult(subscription);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Subscription> ListSubscriptionsAsync(
        ListSubscriptionsQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenant = CurrentTenant;
        foreach (var sub in _subscriptions.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (sub.TenantId != tenant)
                continue;

            if (query.PlanId.HasValue && sub.PlanId != query.PlanId.Value)
                continue;

            if (query.Edition.HasValue && sub.Edition != query.Edition.Value)
                continue;

            yield return sub;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public ValueTask<Subscription> AddAddOnAsync(
        SubscriptionId subscriptionId,
        AddOnId addOnId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_subscriptions.TryGetValue(subscriptionId, out var existing))
            throw new KeyNotFoundException($"Subscription '{subscriptionId.Value}' not found.");

        if (existing.TenantId != CurrentTenant)
            throw new KeyNotFoundException($"Subscription '{subscriptionId.Value}' not found.");

        if (existing.AddOns.Contains(addOnId))
            return ValueTask.FromResult(existing);

        var updated = existing with
        {
            AddOns = [.. existing.AddOns, addOnId]
        };
        _subscriptions[subscriptionId] = updated;
        return ValueTask.FromResult(updated);
    }

    /// <inheritdoc />
    public ValueTask<MeteredUsage> RecordUsageAsync(
        UsageMeterId meterId,
        decimal quantity,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (quantity < 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be non-negative.");

        var tenant = CurrentTenant;
        if (!_meters.TryGetValue(meterId, out var meter))
        {
            // For the in-memory reference impl, auto-create a meter stub scoped
            // to the current tenant so tests and demos can record usage without
            // an explicit meter-provisioning step.
            meter = new UsageMeter
            {
                Id = meterId,
                TenantId = tenant,
                SubscriptionId = new SubscriptionId("unknown"),
                Code = "auto",
                Unit = "unit"
            };
            _meters[meterId] = meter;
        }

        var usage = new MeteredUsage
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            MeterId = meterId,
            Quantity = quantity,
            RecordedAtUtc = DateTime.UtcNow
        };
        _usage[usage.Id] = usage;
        return ValueTask.FromResult(usage);
    }
}
