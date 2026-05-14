using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Sunfish.UICore.Wayfinder.Integrations;
using Xunit;

namespace Sunfish.Blocks.Integrations.Tests;

public sealed class DefaultIntegrationAtlasProviderTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");
    private static readonly PrincipalId TestPrincipal = PrincipalId.FromBytes(new byte[32]);
    private static readonly Signature TestSignature = Signature.FromBytes(new byte[64]);

    private static AtlasSettingSnapshot MakeSnapshot(string path, JsonNode? value)
        => new(path, value, new StandingOrderId(Guid.NewGuid()), DateTimeOffset.UtcNow,
            new AtlasSchemaDescriptor(JsonNode.Parse("{}") !, "Setting", string.Empty, AtlasSettingKind.String));

    private static SignedOperation<AuditPayload> FakeSign(AuditPayload payload, DateTimeOffset at, Guid nonce)
        => new(payload, TestPrincipal, at, nonce, TestSignature);

    private static (DefaultIntegrationAtlasProvider provider, IStandingOrderIssuer issuer,
        IAtlasProjector projector, IAuditTrail auditTrail, IFieldEncryptor encryptor,
        IDecryptCapabilityProvider capabilityProvider, IValidationStatusStore statusStore)
        BuildHarness(
            IEnumerable<IIntegrationSchemaProvider>? schemaProviders = null,
            IEnumerable<IIntegrationProviderValidator>? validators = null,
            AtlasView? atlasView = null)
    {
        var issuer = Substitute.For<IStandingOrderIssuer>();
        var projector = Substitute.For<IAtlasProjector>();
        var auditTrail = Substitute.For<IAuditTrail>();
        var signer = Substitute.For<IOperationSigner>();
        var encryptor = Substitute.For<IFieldEncryptor>();
        var decryptor = Substitute.For<IFieldDecryptor>();
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
        var statusStore = Substitute.For<IValidationStatusStore>();
        var context = Substitute.For<IIntegrationAtlasContext>();
        context.CurrentTenantId.Returns(TenantA);
        context.CurrentActorId.Returns(ActorA);

        signer.SignAsync(Arg.Any<AuditPayload>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ValueTask<SignedOperation<AuditPayload>>(
                FakeSign(ci.ArgAt<AuditPayload>(0), ci.ArgAt<DateTimeOffset>(1), ci.ArgAt<Guid>(2))));

        issuer.IssueAsync(Arg.Any<StandingOrderDraft>(), Arg.Any<ActorId>(), Arg.Any<IAuditTrail>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var draft = ci.ArgAt<StandingOrderDraft>(0);
                return Task.FromResult(new StandingOrder(
                    new StandingOrderId(Guid.NewGuid()),
                    draft.TenantId,
                    ci.ArgAt<ActorId>(1),
                    DateTimeOffset.UtcNow,
                    draft.Scope,
                    draft.Triples,
                    draft.Rationale,
                    draft.ApprovalChain,
                    new AuditRecordId(Guid.NewGuid()),
                    StandingOrderState.Validated));
            });

        projector.ProjectAsync(Arg.Any<TenantId>(), Arg.Any<StandingOrderScope>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<AtlasView>(
                atlasView ?? new AtlasView(TenantA, DateTimeOffset.UtcNow, new Dictionary<string, AtlasSettingSnapshot>())));

        statusStore.GetCurrentAsync(Arg.Any<TenantId>(), Arg.Any<IntegrationCategory>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderValidationStatusEntry?>(null));
        statusStore.HistoryAsync(Arg.Any<TenantId>(), Arg.Any<IntegrationCategory>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ProviderValidationStatusEntry>());

        var provider = new DefaultIntegrationAtlasProvider(
            issuer, projector, auditTrail, signer,
            encryptor, decryptor, capabilityProvider, statusStore, context,
            schemaProviders ?? Array.Empty<IIntegrationSchemaProvider>(),
            validators ?? Array.Empty<IIntegrationProviderValidator>());

        return (provider, issuer, projector, auditTrail, encryptor, capabilityProvider, statusStore);
    }

    [Fact]
    public void Constructor_DuplicateValidator_ThrowsDuplicateValidatorRegistrationException()
    {
        var v1 = Substitute.For<IIntegrationProviderValidator>();
        v1.SupportedCategory.Returns(IntegrationCategory.TransactionalEmail);
        v1.SupportedProvider.Returns("sendgrid");
        var v2 = Substitute.For<IIntegrationProviderValidator>();
        v2.SupportedCategory.Returns(IntegrationCategory.TransactionalEmail);
        v2.SupportedProvider.Returns("sendgrid");

        var ex = Assert.Throws<DuplicateValidatorRegistrationException>(() =>
            new DefaultIntegrationAtlasProvider(
                Substitute.For<IStandingOrderIssuer>(),
                Substitute.For<IAtlasProjector>(),
                Substitute.For<IAuditTrail>(),
                Substitute.For<IOperationSigner>(),
                Substitute.For<IFieldEncryptor>(),
                Substitute.For<IFieldDecryptor>(),
                Substitute.For<IDecryptCapabilityProvider>(),
                Substitute.For<IValidationStatusStore>(),
                Substitute.For<IIntegrationAtlasContext>(),
                Array.Empty<IIntegrationSchemaProvider>(),
                new[] { v1, v2 }));

        Assert.Equal(IntegrationCategory.TransactionalEmail, ex.Category);
        Assert.Equal("sendgrid", ex.ProviderId);
    }

    [Fact]
    public void GetSchemas_ReturnsSchemasFromAllProviders()
    {
        var schema1 = new IntegrationProviderSchema("sendgrid", "SendGrid", IntegrationCategory.TransactionalEmail, Array.Empty<CredentialFieldSpec>(), null, null);
        var schema2 = new IntegrationProviderSchema("twilio", "Twilio", IntegrationCategory.Messaging, Array.Empty<CredentialFieldSpec>(), null, null);

        var schemaProvider1 = Substitute.For<IIntegrationSchemaProvider>();
        schemaProvider1.GetSchemas().Returns(new[] { schema1 });
        var schemaProvider2 = Substitute.For<IIntegrationSchemaProvider>();
        schemaProvider2.GetSchemas().Returns(new[] { schema2 });

        var (provider, _, _, _, _, _, _) = BuildHarness(schemaProviders: new[] { schemaProvider1, schemaProvider2 });

        var schemas = provider.GetSchemas();

        Assert.Equal(2, schemas.Count);
        Assert.Contains(schemas, s => s.ProviderId == "sendgrid");
        Assert.Contains(schemas, s => s.ProviderId == "twilio");
    }

    [Fact]
    public async Task IssueProviderChangeAsync_IssuesToStandingOrderIssuer()
    {
        var (provider, issuer, _, _, _, _, _) = BuildHarness();

        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        var orderId = await provider.IssueProviderChangeAsync(IntegrationCategory.TransactionalEmail, "sendgrid", ctx);

        Assert.NotEqual(default, orderId);
        await issuer.Received(1).IssueAsync(
            Arg.Is<StandingOrderDraft>(d => d!.Scope == StandingOrderScope.Integration && d!.TenantId == TenantA),
            ActorA,
            Arg.Any<IAuditTrail>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueNonSensitiveCredentialAsync_IssuesToStandingOrderIssuer()
    {
        var (provider, issuer, _, _, _, _, _) = BuildHarness();

        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        var orderId = await provider.IssueNonSensitiveCredentialAsync(
            IntegrationCategory.TransactionalEmail, "sendgrid", "fromName",
            JsonValue.Create("My App"), ctx);

        Assert.NotEqual(default, orderId);
        await issuer.Received(1).IssueAsync(
            Arg.Is<StandingOrderDraft>(d =>
                d!.Scope == StandingOrderScope.Integration &&
                d!.TenantId == TenantA &&
                d!.Triples.Count == 1),
            ActorA,
            Arg.Any<IAuditTrail>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueRoutingAsync_IssuesToStandingOrderIssuer()
    {
        var (provider, issuer, _, _, _, _, _) = BuildHarness();

        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        var routing = new IntegrationEmailRouting("sendgrid", "mailchimp");
        var orderId = await provider.IssueRoutingAsync(routing, ctx);

        Assert.NotEqual(default, orderId);
        await issuer.Received(1).IssueAsync(
            Arg.Is<StandingOrderDraft>(d => d!.Scope == StandingOrderScope.Integration),
            ActorA,
            Arg.Any<IAuditTrail>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateProviderAsync_NoActiveProvider_ReturnsUnknownWithNoValidatorCode()
    {
        var capability = Substitute.For<IDecryptCapability>();
        var (provider, _, _, _, _, capabilityProvider, _) = BuildHarness();
        capabilityProvider.AcquireAsync(TenantA, Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDecryptCapability?>(capability));

        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        // Empty atlas — no active provider configured
        var result = await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, ctx);

        Assert.Equal(ProviderValidationStatus.Unknown, result.Status);
        Assert.Equal("no-validator-registered", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateProviderAsync_MissingValidator_ReturnsUnknownWithNoValidatorCode()
    {
        var capability = Substitute.For<IDecryptCapability>();
        var settings = new Dictionary<string, AtlasSettingSnapshot>
        {
            ["integration:integration/TransactionalEmail/activeProvider"] = MakeSnapshot(
                "integration/TransactionalEmail/activeProvider", JsonValue.Create("sendgrid"))
        };

        var (provider, _, _, _, _, capabilityProvider, _) = BuildHarness(
            atlasView: new AtlasView(TenantA, DateTimeOffset.UtcNow, settings));
        capabilityProvider.AcquireAsync(TenantA, Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDecryptCapability?>(capability));

        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        // No validator registered for (TransactionalEmail, sendgrid)
        var result = await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, ctx);

        Assert.Equal(ProviderValidationStatus.Unknown, result.Status);
        Assert.Equal("no-validator-registered", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateProviderAsync_HappyPath_CallsValidatorAndStoresResult()
    {
        var capability = Substitute.For<IDecryptCapability>();
        var settings = new Dictionary<string, AtlasSettingSnapshot>
        {
            ["integration:integration/TransactionalEmail/activeProvider"] = MakeSnapshot(
                "integration/TransactionalEmail/activeProvider", JsonValue.Create("sendgrid"))
        };

        var validator = Substitute.For<IIntegrationProviderValidator>();
        validator.SupportedCategory.Returns(IntegrationCategory.TransactionalEmail);
        validator.SupportedProvider.Returns("sendgrid");
        validator.ValidateAsync(
            Arg.Any<IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>>>(),
            Arg.Any<IReadOnlyDictionary<string, JsonNode>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new IntegrationValidationResult(
                ProviderValidationStatus.Valid, DateTimeOffset.UtcNow, null, null)));

        var (provider, _, _, _, _, capabilityProvider, statusStore) = BuildHarness(
            validators: new[] { validator },
            atlasView: new AtlasView(TenantA, DateTimeOffset.UtcNow, settings));
        capabilityProvider.AcquireAsync(TenantA, Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDecryptCapability?>(capability));

        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        var result = await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, ctx);

        Assert.Equal(ProviderValidationStatus.Valid, result.Status);
        await statusStore.Received().UpdateAsync(
            TenantA, IntegrationCategory.TransactionalEmail, "sendgrid",
            Arg.Is<IntegrationValidationResult>(r => r!.Status == ProviderValidationStatus.Valid),
            ActorA,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InMemoryIntegrationAtlasProvider_HappyPath_FullLifecycle()
    {
        var inMemory = new InMemoryIntegrationAtlasProvider();
        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        var orderId = await inMemory.IssueProviderChangeAsync(IntegrationCategory.TransactionalEmail, "sendgrid", ctx);
        Assert.NotEqual(default, orderId);

        var credOrderId = await inMemory.IssueSensitiveCredentialAsync(
            IntegrationCategory.TransactionalEmail, "sendgrid", "apiKey",
            new System.ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }), ctx);
        Assert.NotEqual(default, credOrderId);

        Assert.Equal((IntegrationCategory.TransactionalEmail, "apiKey"), inMemory.LastSensitiveCredentialUpdate);
    }
}

internal static class AsyncEnumerable
{
    public static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
