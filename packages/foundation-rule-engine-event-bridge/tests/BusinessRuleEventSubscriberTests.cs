using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.BusinessLogic;
using Sunfish.Foundation.BusinessLogic.Rules;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.RuleEngine.EventBridge;
using Sunfish.Foundation.RuleEngine.EventBridge.DependencyInjection;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.Events.DependencyInjection;

namespace Sunfish.Foundation.RuleEngine.EventBridge.Tests;

/// <summary>
/// Coverage for <see cref="BusinessRuleEventSubscriber"/>:
/// rule fires on matching event; unrelated event does not trigger rule;
/// subscriber disposes cleanly; DI wiring resolves a hosted service.
/// </summary>
public sealed class BusinessRuleEventSubscriberTests : IDisposable
{
    private readonly InMemoryEventBus _bus;
    private readonly Ed25519Signer _signer;
    private readonly KeyPair _keyPair;

    public BusinessRuleEventSubscriberTests()
    {
        var verifier = new Ed25519Verifier();
        _keyPair = KeyPair.Generate();
        _signer = new Ed25519Signer(_keyPair);
        _bus = new InMemoryEventBus(verifier);
    }

    public void Dispose() => _keyPair.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────

    private static KernelEvent BuildEvent(string entityId, string kind,
        Dictionary<string, object?>? payload = null)
        => new(
            Id: EventId.NewId(),
            EntityId: EntityId.Parse(entityId),
            Kind: kind,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: payload ?? new Dictionary<string, object?> { ["v"] = 1 });

    private ValueTask<SignedOperation<KernelEvent>> SignAsync(KernelEvent evt)
        => _signer.SignAsync(evt, DateTimeOffset.UtcNow, Guid.NewGuid());

    /// <summary>
    /// A simple <see cref="IBusinessRule"/> that operates directly on a
    /// <see cref="KernelEvent"/> — no FieldManager / BusinessObjectBase required.
    /// </summary>
    private sealed class BannedKeyRule : IBusinessRule
    {
        public string? PropertyName => "Payload";

        public string? Validate(object businessObject)
        {
            if (businessObject is not KernelEvent evt) return null;
            return evt.Payload.ContainsKey("banned") ? "Payload contains banned key" : null;
        }
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subscriber_WhenEventPublished_EvaluatesRulesAndInvokesCallback()
    {
        // Arrange
        var engine = new BusinessRuleEngine();
        engine.AddRule(new BannedKeyRule());

        var brokenRulesReceived = new List<(KernelEvent Event, IReadOnlyList<BrokenRule> Broken)>();
        var subscription = new EventSubscription("rule-sub");

        using var subscriber = new BusinessRuleEventSubscriber(
            _bus, engine, subscription,
            onEvaluated: (evt, broken) => brokenRulesReceived.Add((evt, broken)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var startTask = subscriber.StartAsync(cts.Token);

        // Let the background loop register its subscription before publishing.
        await Task.Delay(50, cts.Token);

        // Act — event whose payload triggers the rule
        var badEvent = BuildEvent("property:acme/1", "entity.created",
            new Dictionary<string, object?> { ["banned"] = true });
        await _bus.PublishAsync(await SignAsync(badEvent), cts.Token);

        // Give the subscriber time to process
        await Task.Delay(200, CancellationToken.None);
        cts.Cancel();
        try { await startTask; } catch (OperationCanceledException) { }

        // Assert
        Assert.Single(brokenRulesReceived);
        var (receivedEvt, broken) = brokenRulesReceived[0];
        Assert.Equal(badEvent.Id, receivedEvt.Id);
        Assert.Single(broken);
        Assert.Equal("Payload contains banned key", broken[0].Message);
    }

    [Fact]
    public async Task Subscriber_WhenEventPublished_ValidPayload_CallbackReceivesEmptyBrokenRules()
    {
        // Arrange
        var engine = new BusinessRuleEngine();
        engine.AddRule(new BannedKeyRule());

        var evaluations = new List<IReadOnlyList<BrokenRule>>();
        var subscription = new EventSubscription("rule-sub-valid");
        using var subscriber = new BusinessRuleEventSubscriber(
            _bus, engine, subscription,
            onEvaluated: (_, broken) => evaluations.Add(broken));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var startTask = subscriber.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token);

        // Act — valid payload (no "banned" key)
        var goodEvent = BuildEvent("property:acme/1", "entity.created",
            new Dictionary<string, object?> { ["allowed"] = true });
        await _bus.PublishAsync(await SignAsync(goodEvent), cts.Token);

        await Task.Delay(200, CancellationToken.None);
        cts.Cancel();
        try { await startTask; } catch (OperationCanceledException) { }

        // Assert — callback was called but no broken rules
        Assert.Single(evaluations);
        Assert.Empty(evaluations[0]);
    }

    [Fact]
    public async Task Subscriber_WithKindFilter_DoesNotTriggerOnUnrelatedKind()
    {
        // Arrange — subscribe to "entity.created" only; rule would fire on any event
        var engine = new BusinessRuleEngine();
        engine.AddRule(new BannedKeyRule());

        var evaluations = new List<(KernelEvent Event, IReadOnlyList<BrokenRule> Broken)>();
        // KindFilter: only "entity.created" events flow through
        var subscription = new EventSubscription("rule-sub-filtered", KindFilter: "entity.created");
        using var subscriber = new BusinessRuleEventSubscriber(
            _bus, engine, subscription,
            onEvaluated: (evt, broken) => evaluations.Add((evt, broken)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var startTask = subscriber.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token);

        // Act — first publish a kind that is filtered out, then the matching kind
        await _bus.PublishAsync(await SignAsync(BuildEvent("property:acme/1", "entity.deleted")), cts.Token);
        await _bus.PublishAsync(await SignAsync(BuildEvent("property:acme/1", "entity.created")), cts.Token);

        await Task.Delay(200, CancellationToken.None);
        cts.Cancel();
        try { await startTask; } catch (OperationCanceledException) { }

        // Assert — only the matching kind reached the subscriber/engine
        Assert.Single(evaluations);
        Assert.Equal("entity.created", evaluations[0].Event.Kind);
    }

    [Fact]
    public async Task Subscriber_WithExtractor_EvaluatesRulesAgainstExtractedObject()
    {
        // Arrange — extractor maps KernelEvent → string kind for evaluation.
        // A rule checks the kind directly on the extracted string object.
        var engine = new BusinessRuleEngine();
        engine.AddRule(new ForbidDeletedKindRule());

        var evaluations = new List<IReadOnlyList<BrokenRule>>();
        var subscription = new EventSubscription("rule-sub-extractor");

        // Extractor: extract the Kind string from the event
        using var subscriber = new BusinessRuleEventSubscriber(
            _bus, engine, subscription,
            onEvaluated: (_, broken) => evaluations.Add(broken),
            extractor: evt => evt.Kind);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var startTask = subscriber.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token);

        // Act — publish a "forbidden" kind and a safe kind
        await _bus.PublishAsync(await SignAsync(BuildEvent("property:acme/1", "entity.deleted")), cts.Token);
        await _bus.PublishAsync(await SignAsync(BuildEvent("property:acme/1", "entity.created")), cts.Token);

        await Task.Delay(200, CancellationToken.None);
        cts.Cancel();
        try { await startTask; } catch (OperationCanceledException) { }

        // Assert — two evaluations; only the deleted one has a broken rule
        Assert.Equal(2, evaluations.Count);
        Assert.Single(evaluations[0]);  // entity.deleted — broken
        Assert.Empty(evaluations[1]);   // entity.created — clean
        Assert.Equal("entity.deleted kind is forbidden", evaluations[0][0].Message);
    }

    [Fact]
    public async Task Subscriber_WhenCancelled_DisposesCleanlyWithoutThrowingUnhandled()
    {
        // Arrange
        var engine = new BusinessRuleEngine();
        var subscription = new EventSubscription("rule-sub-dispose");
        using var subscriber = new BusinessRuleEventSubscriber(
            _bus, engine, subscription, onEvaluated: null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var startTask = subscriber.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token);

        // Act — cancel immediately
        cts.Cancel();

        // Assert — no unhandled exception; StopAsync completes cleanly
        var exception = await Record.ExceptionAsync(async () =>
        {
            try { await startTask; } catch (OperationCanceledException) { /* expected */ }
            await subscriber.StopAsync(CancellationToken.None);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddBusinessRuleSubscriber_DI_ResolvesHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var verifier = new Ed25519Verifier();
        var keyPair = KeyPair.Generate();
        services.AddSingleton<IOperationVerifier>(verifier);
        services.AddSunfishKernelEventBus();
        services.AddSingleton(new BusinessRuleEngine());
        services.AddBusinessRuleSubscriber(
            subscription: new EventSubscription("di-sub"),
            onEvaluated: null);

        var provider = services.BuildServiceProvider();

        // Act
        var hostedServices = provider.GetServices<IHostedService>();

        // Assert — at least one BusinessRuleEventSubscriber is registered
        Assert.Contains(hostedServices, s => s is BusinessRuleEventSubscriber);

        keyPair.Dispose();
    }

    // ── Additional rule helper for extractor test ─────────────────────────

    private sealed class ForbidDeletedKindRule : IBusinessRule
    {
        public string? PropertyName => null; // object-level rule

        public string? Validate(object businessObject)
        {
            if (businessObject is not string kind) return null;
            return kind == "entity.deleted" ? "entity.deleted kind is forbidden" : null;
        }
    }
}
