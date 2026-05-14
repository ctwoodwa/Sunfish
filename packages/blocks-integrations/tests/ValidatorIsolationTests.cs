using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Sunfish.UICore.Wayfinder.Integrations;
using Xunit;

namespace Sunfish.Blocks.Integrations.Tests;

/// <summary>
/// ADR 0067 §6.2 — validator isolation: ValidateProviderAsync passes only
/// decrypted byte collections and JSON nodes to validators, not domain services.
/// </summary>
public sealed class ValidatorIsolationTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");
    private static readonly PrincipalId TestPrincipal = PrincipalId.FromBytes(new byte[32]);
    private static readonly Signature TestSignature = Signature.FromBytes(new byte[64]);

    private static AtlasSettingSnapshot MakeSnapshot(string path, JsonNode? value)
        => new(path, value, new StandingOrderId(Guid.NewGuid()), DateTimeOffset.UtcNow,
            new AtlasSchemaDescriptor(JsonNode.Parse("{}") !, "Setting", string.Empty, AtlasSettingKind.String));

    [Fact]
    public async Task ValidateProviderAsync_DoesNotCallIssuer_DuringValidation()
    {
        var capability = Substitute.For<IDecryptCapability>();
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
        capabilityProvider.AcquireAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDecryptCapability?>(capability));

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

        var issuer = Substitute.For<IStandingOrderIssuer>();
        var projector = Substitute.For<IAtlasProjector>();
        projector.ProjectAsync(Arg.Any<TenantId>(), Arg.Any<StandingOrderScope>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<AtlasView>(new AtlasView(TenantA, DateTimeOffset.UtcNow, settings)));

        var auditTrail = Substitute.For<IAuditTrail>();
        var signer = Substitute.For<IOperationSigner>();
        signer.SignAsync(Arg.Any<AuditPayload>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ValueTask<SignedOperation<AuditPayload>>(
                new SignedOperation<AuditPayload>(
                    ci.ArgAt<AuditPayload>(0), TestPrincipal, ci.ArgAt<DateTimeOffset>(1), ci.ArgAt<Guid>(2), TestSignature)));
        var statusStore = Substitute.For<IValidationStatusStore>();
        statusStore.GetCurrentAsync(Arg.Any<TenantId>(), Arg.Any<IntegrationCategory>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderValidationStatusEntry?>(null));
        statusStore.HistoryAsync(Arg.Any<TenantId>(), Arg.Any<IntegrationCategory>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ProviderValidationStatusEntry>());
        var context = Substitute.For<IIntegrationAtlasContext>();
        context.CurrentTenantId.Returns(TenantA);
        context.CurrentActorId.Returns(ActorA);

        var provider = new DefaultIntegrationAtlasProvider(
            issuer, projector, auditTrail, signer,
            Substitute.For<IFieldEncryptor>(),
            Substitute.For<IFieldDecryptor>(),
            capabilityProvider, statusStore, context,
            Array.Empty<IIntegrationSchemaProvider>(),
            new[] { validator });

        await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, context);

        // IStandingOrderIssuer.IssueAsync must NOT be called during validation
        await issuer.DidNotReceive().IssueAsync(
            Arg.Any<StandingOrderDraft>(),
            Arg.Any<ActorId>(),
            Arg.Any<IAuditTrail>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateProviderAsync_PassesOnlyPrimitiveCollectionsToValidator()
    {
        var capability = Substitute.For<IDecryptCapability>();
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
        capabilityProvider.AcquireAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDecryptCapability?>(capability));

        var settings = new Dictionary<string, AtlasSettingSnapshot>
        {
            ["integration:integration/TransactionalEmail/activeProvider"] = MakeSnapshot(
                "integration/TransactionalEmail/activeProvider", JsonValue.Create("sendgrid"))
        };

        IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>>? capturedSensitive = null;
        IReadOnlyDictionary<string, JsonNode>? capturedNonSensitive = null;

        var validator = Substitute.For<IIntegrationProviderValidator>();
        validator.SupportedCategory.Returns(IntegrationCategory.TransactionalEmail);
        validator.SupportedProvider.Returns("sendgrid");
        validator.ValidateAsync(
            Arg.Any<IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>>>(),
            Arg.Any<IReadOnlyDictionary<string, JsonNode>>(),
            Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedSensitive = ci.ArgAt<IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>>>(0);
                capturedNonSensitive = ci.ArgAt<IReadOnlyDictionary<string, JsonNode>>(1);
                return Task.FromResult(new IntegrationValidationResult(
                    ProviderValidationStatus.Valid, DateTimeOffset.UtcNow, null, null));
            });

        var issuer = Substitute.For<IStandingOrderIssuer>();
        var projector = Substitute.For<IAtlasProjector>();
        projector.ProjectAsync(Arg.Any<TenantId>(), Arg.Any<StandingOrderScope>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<AtlasView>(new AtlasView(TenantA, DateTimeOffset.UtcNow, settings)));
        var auditTrail = Substitute.For<IAuditTrail>();
        var signer = Substitute.For<IOperationSigner>();
        signer.SignAsync(Arg.Any<AuditPayload>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ValueTask<SignedOperation<AuditPayload>>(
                new SignedOperation<AuditPayload>(
                    ci.ArgAt<AuditPayload>(0), TestPrincipal, ci.ArgAt<DateTimeOffset>(1), ci.ArgAt<Guid>(2), TestSignature)));
        var statusStore = Substitute.For<IValidationStatusStore>();
        statusStore.GetCurrentAsync(Arg.Any<TenantId>(), Arg.Any<IntegrationCategory>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderValidationStatusEntry?>(null));
        statusStore.HistoryAsync(Arg.Any<TenantId>(), Arg.Any<IntegrationCategory>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ProviderValidationStatusEntry>());
        var context = Substitute.For<IIntegrationAtlasContext>();
        context.CurrentTenantId.Returns(TenantA);
        context.CurrentActorId.Returns(ActorA);

        var provider = new DefaultIntegrationAtlasProvider(
            issuer, projector, auditTrail, signer,
            Substitute.For<IFieldEncryptor>(),
            Substitute.For<IFieldDecryptor>(),
            capabilityProvider, statusStore, context,
            Array.Empty<IIntegrationSchemaProvider>(),
            new[] { validator });

        await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, context);

        Assert.NotNull(capturedSensitive);
        Assert.NotNull(capturedNonSensitive);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>>>(capturedSensitive);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonNode>>(capturedNonSensitive);
    }
}
