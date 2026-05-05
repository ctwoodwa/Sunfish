using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Catalog.ExtensionFields;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Extensibility;
using Sunfish.Foundation.FeatureManagement;
using Sunfish.Foundation.Migration;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Catalog.Tests.ExtensionFields;

public class ExtensionFieldGatingTests
{
    private static readonly TenantId Tenant = new("tenant-1");
    private static readonly Type EntityType = typeof(SampleEntity);
    private static readonly ExtensionFieldKey UngatedKey = new("ungated.field");
    private static readonly ExtensionFieldKey GatedKey = new("gated.field");
    private static readonly FeatureKey GateKey = FeatureKey.Of("gated.feature");

    [Fact]
    public async Task A_NoFeatureKey_ReturnsUngated_RegardlessOfEvaluatorState()
    {
        var evaluator = Substitute.For<IFeatureEvaluator>();
        evaluator.IsEnabledAsync(Arg.Any<FeatureKey>(), Arg.Any<FeatureEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var catalog = NewCatalog(featureEvaluator: evaluator);
        catalog.Register(EntityType, NewSpec(UngatedKey, featureKey: null));

        var result = await catalog.GetFieldsAsync(EntityType, CtxFor(Tenant));

        Assert.Single(result);
        Assert.Equal(GateState.Ungated, result[0].GateState);
        await evaluator.DidNotReceive().IsEnabledAsync(
            Arg.Any<FeatureKey>(), Arg.Any<FeatureEvaluationContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task B_GatedSpec_GateOn_ReturnsGatedOn_AndEmitsExtensionFieldGated()
    {
        var (evaluator, audit, signer) = SetupOnlineGate(gateOn: true);
        var catalog = NewCatalog(featureEvaluator: evaluator, auditTrail: audit, signer: signer);
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey));

        var result = await catalog.GetFieldsAsync(EntityType, CtxFor(Tenant));

        Assert.Single(result);
        Assert.Equal(GateState.GatedOn, result[0].GateState);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.ExtensionFieldGated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task C_GatedOff_PolicyHide_ExcludedAndEmitsFiltered()
    {
        var (evaluator, audit, signer) = SetupOnlineGate(gateOn: false);
        var catalog = NewCatalog(featureEvaluator: evaluator, auditTrail: audit, signer: signer);
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey, policy: FeatureGateOffPolicy.Hide));

        var result = await catalog.GetFieldsAsync(EntityType, CtxFor(Tenant));

        Assert.Empty(result);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.ExtensionFieldFiltered),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task D_GatedOff_PolicySequester_CallsStoreAndEmitsSequestered()
    {
        var (evaluator, audit, signer) = SetupOnlineGate(gateOn: false);
        var sequester = Substitute.For<ISequestrationStore>();
        var catalog = NewCatalog(
            featureEvaluator: evaluator, auditTrail: audit, signer: signer, sequestrationStore: sequester);
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey, policy: FeatureGateOffPolicy.Sequester));

        var result = await catalog.GetFieldsAsync(EntityType, CtxFor(Tenant));

        Assert.Empty(result);
        await sequester.Received(1).SequesterAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s != null && s.Contains(GatedKey.Value)),
            SequestrationFlagKind.FeatureGateOff,
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.ExtensionFieldSequestered),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task E1_GatedOff_PolicyRedact_CapabilityGranted_TombstonePathAndEmitsRedacted()
    {
        var (evaluator, audit, signer) = SetupOnlineGate(gateOn: false);
        var capabilityGraph = Substitute.For<ICapabilityGraph>();
        capabilityGraph.QueryAsync(
            Arg.Any<PrincipalId>(), Arg.Any<Resource>(),
            Arg.Is<CapabilityAction>(a => a.Name == "redact-extension-field"),
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var catalog = NewCatalog(
            featureEvaluator: evaluator, auditTrail: audit, signer: signer, capabilityGraph: capabilityGraph);
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey, policy: FeatureGateOffPolicy.Redact));

        var result = await catalog.GetFieldsAsync(EntityType, CtxFor(Tenant));

        Assert.Empty(result);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.ExtensionFieldRedacted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task E2_GatedOff_PolicyRedact_CapabilityDenied_ThrowsAndEmitsRedacted()
    {
        var (evaluator, audit, signer) = SetupOnlineGate(gateOn: false);
        var capabilityGraph = Substitute.For<ICapabilityGraph>();
        capabilityGraph.QueryAsync(
            Arg.Any<PrincipalId>(), Arg.Any<Resource>(), Arg.Any<CapabilityAction>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var catalog = NewCatalog(
            featureEvaluator: evaluator, auditTrail: audit, signer: signer, capabilityGraph: capabilityGraph);
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey, policy: FeatureGateOffPolicy.Redact));

        await Assert.ThrowsAsync<ExtensionFieldRedactionDeniedException>(
            () => catalog.GetFieldsAsync(EntityType, CtxFor(Tenant)).AsTask());
        // The audit MUST be emitted BEFORE the throw so the denial is recorded.
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.ExtensionFieldRedacted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task E3_GatedOff_PolicyRedact_NullCapabilityGraph_ThrowsInvalidOperation()
    {
        var (evaluator, _, _) = SetupOnlineGate(gateOn: false);
        var catalog = NewCatalog(featureEvaluator: evaluator); // no capability graph or signer
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey, policy: FeatureGateOffPolicy.Redact));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => catalog.GetFieldsAsync(EntityType, CtxFor(Tenant)).AsTask());
    }

    [Fact]
    public async Task F_NullEvaluator_AllSpecsReturnedAsUngated()
    {
        var catalog = NewCatalog(featureEvaluator: null);
        catalog.Register(EntityType, NewSpec(UngatedKey, featureKey: null));
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey));

        var result = await catalog.GetFieldsAsync(EntityType, CtxFor(Tenant));

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Equal(GateState.Ungated, m.GateState));
    }

    [Fact]
    public void G_RedactExtensionFieldAction_NameInvariant()
    {
        // Static assertion: if anyone changes the capability-action name, this test
        // fails and forces a deliberate cohort review (capability-graph rules in
        // production reference this name).
        var fieldsWithRedactAction = typeof(ExtensionFieldCatalog).GetField(
            "RedactExtensionFieldAction",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(fieldsWithRedactAction);
        var action = (CapabilityAction)fieldsWithRedactAction!.GetValue(null)!;
        Assert.Equal("redact-extension-field", action.Name);
    }

    [Fact]
    public async Task H_EvaluatorThrows_GateTreatedAsOff_AndEmitsGateEvaluationFailed()
    {
        var evaluator = Substitute.For<IFeatureEvaluator>();
        evaluator.IsEnabledAsync(Arg.Any<FeatureKey>(), Arg.Any<FeatureEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("evaluator unavailable"));
        var audit = Substitute.For<IAuditTrail>();
        var signer = NewSigner();
        var catalog = NewCatalog(featureEvaluator: evaluator, auditTrail: audit, signer: signer);
        // Default policy Hide → field excluded from output after fail-closed.
        catalog.Register(EntityType, NewSpec(GatedKey, featureKey: GateKey, policy: FeatureGateOffPolicy.Hide));

        var result = await catalog.GetFieldsAsync(EntityType, CtxFor(Tenant));

        Assert.Empty(result);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.ExtensionFieldGateEvaluationFailed),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.ExtensionFieldFiltered),
            Arg.Any<CancellationToken>());
    }

    // ---- helpers ----

    private static FeatureEvaluationContext CtxFor(TenantId tenantId) =>
        new() { TenantId = tenantId };

    private static ExtensionFieldSpec NewSpec(
        ExtensionFieldKey key,
        FeatureKey? featureKey,
        FeatureGateOffPolicy policy = FeatureGateOffPolicy.Hide)
        => new(
            Key: key,
            ValueType: typeof(string),
            Scope: ExtensionFieldScope.Bundle,
            Storage: ExtensionStorage.Json,
            FeatureKey: featureKey,
            FeatureGateOffPolicy: policy);

    private static (IFeatureEvaluator evaluator, IAuditTrail audit, IOperationSigner signer) SetupOnlineGate(bool gateOn)
    {
        var evaluator = Substitute.For<IFeatureEvaluator>();
        evaluator.IsEnabledAsync(Arg.Any<FeatureKey>(), Arg.Any<FeatureEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(gateOn);
        var audit = Substitute.For<IAuditTrail>();
        var signer = NewSigner();
        return (evaluator, audit, signer);
    }

    private static IOperationSigner NewSigner()
    {
        var signer = Substitute.For<IOperationSigner>();
        var principalId = PrincipalId.FromBytes(new byte[32]);
        signer.IssuerId.Returns(principalId);
        // Stub SignAsync to return a stable SignedOperation envelope using NSec-free defaults.
        signer.SignAsync(
            Arg.Any<AuditPayload>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var payload = call.Arg<AuditPayload>();
                var issuedAt = call.Arg<DateTimeOffset>();
                var nonce = call.Arg<Guid>();
                return new ValueTask<SignedOperation<AuditPayload>>(new SignedOperation<AuditPayload>(
                    Payload: payload!,
                    IssuerId: principalId,
                    IssuedAt: issuedAt,
                    Nonce: nonce,
                    Signature: Signature.FromBytes(new byte[64])));
            });
        return signer;
    }

    private static ExtensionFieldCatalog NewCatalog(
        IFeatureEvaluator? featureEvaluator = null,
        IAuditTrail? auditTrail = null,
        ISequestrationStore? sequestrationStore = null,
        ICapabilityGraph? capabilityGraph = null,
        IOperationSigner? signer = null,
        TimeProvider? clock = null)
        => new(featureEvaluator, auditTrail, sequestrationStore, capabilityGraph, signer, clock);

    private sealed class SampleEntity : IHasExtensionData
    {
        public ExtensionDataBag Extensions { get; } = new();
    }
}
