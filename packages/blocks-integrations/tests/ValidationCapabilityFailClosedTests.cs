using System;
using System.Collections.Generic;
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
/// ADR 0067 §5.3.1 — 3 fail-closed failure modes for capability acquisition.
/// </summary>
public sealed class ValidationCapabilityFailClosedTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");
    private static readonly PrincipalId TestPrincipal = PrincipalId.FromBytes(new byte[32]);
    private static readonly Signature TestSignature = Signature.FromBytes(new byte[64]);

    private static DefaultIntegrationAtlasProvider BuildProvider(
        IDecryptCapabilityProvider capabilityProvider)
    {
        var issuer = Substitute.For<IStandingOrderIssuer>();
        var projector = Substitute.For<IAtlasProjector>();
        projector.ProjectAsync(Arg.Any<TenantId>(), Arg.Any<StandingOrderScope>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<AtlasView>(
                new AtlasView(TenantA, DateTimeOffset.UtcNow, new Dictionary<string, AtlasSettingSnapshot>())));
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

        return new DefaultIntegrationAtlasProvider(
            issuer, projector, auditTrail, signer,
            Substitute.For<Foundation.Recovery.Crypto.IFieldEncryptor>(),
            Substitute.For<Foundation.Recovery.Crypto.IFieldDecryptor>(),
            capabilityProvider, statusStore, context,
            Array.Empty<IIntegrationSchemaProvider>(),
            Array.Empty<IIntegrationProviderValidator>());
    }

    [Fact]
    public async Task AcquireAsync_ReturnsNull_ResultIsUnknownWithNoDecryptCapability()
    {
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
        capabilityProvider.AcquireAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDecryptCapability?>(null));

        var provider = BuildProvider(capabilityProvider);
        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        var result = await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, ctx);

        Assert.Equal(ProviderValidationStatus.Unknown, result.Status);
        Assert.Equal("no-decrypt-capability", result.ErrorCode);
    }

    [Fact]
    public async Task AcquireAsync_Throws_ResultIsUnknownWithNoDecryptCapability_DoesNotRethrow()
    {
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
        capabilityProvider.AcquireAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<IDecryptCapability?>>(_ => throw new InvalidOperationException("key store unavailable"));

        var provider = BuildProvider(capabilityProvider);
        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        var result = await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, ctx);

        Assert.Equal(ProviderValidationStatus.Unknown, result.Status);
        Assert.Equal("no-decrypt-capability", result.ErrorCode);
    }

    [Fact]
    public async Task AcquireAsync_Cancellation_IsPropagated()
    {
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
        capabilityProvider.AcquireAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<IDecryptCapability?>>(_ => throw new OperationCanceledException());

        var provider = BuildProvider(capabilityProvider);
        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, ctx));
    }

    [Fact]
    public async Task DecryptFailure_ReturnsUnknownWithDecryptFailedCode_NotFail_Open()
    {
        // Verifies fail-closed on decrypt failure: a credential that cannot be decrypted
        // must NOT result in validation running against a partial credential set.
        var capability = Substitute.For<IDecryptCapability>();
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
        capabilityProvider.AcquireAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDecryptCapability?>(capability));

        // Atlas has an active provider + an encrypted credential entry
        var orderId = new StandingOrderId(Guid.NewGuid());
        var schema = new AtlasSchemaDescriptor(
            System.Text.Json.Nodes.JsonNode.Parse("{}") !,
            "Setting", string.Empty, AtlasSettingKind.String);
        var fakeEncryptedJson = System.Text.Json.Nodes.JsonNode.Parse(
            "{\"ct\":\"AAAAAAAAAAAAAAAAAAAAAA==\",\"nonce\":\"AAAAAAAAAAAAAAA=\",\"kv\":1}");

        var settings = new Dictionary<string, AtlasSettingSnapshot>
        {
            ["integration:integration/TransactionalEmail/activeProvider"] = new(
                "integration/TransactionalEmail/activeProvider",
                System.Text.Json.Nodes.JsonValue.Create("sendgrid"),
                orderId, DateTimeOffset.UtcNow, schema),
            ["integration:integration/TransactionalEmail/credential/apiKey.encrypted"] = new(
                "integration/TransactionalEmail/credential/apiKey.encrypted",
                fakeEncryptedJson, orderId, DateTimeOffset.UtcNow, schema),
        };

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

        // Decryptor always throws — simulates key unavailability / corrupt ciphertext
        var decryptor = Substitute.For<Foundation.Recovery.Crypto.IFieldDecryptor>();
        decryptor.DecryptAsync(Arg.Any<Foundation.Recovery.EncryptedField>(), Arg.Any<Foundation.Crypto.IDecryptCapability>(), Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns<Task<System.ReadOnlyMemory<byte>>>(_ => throw new InvalidOperationException("key not found"));

        var validator = Substitute.For<IIntegrationProviderValidator>();
        validator.SupportedCategory.Returns(IntegrationCategory.TransactionalEmail);
        validator.SupportedProvider.Returns("sendgrid");

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
            Substitute.For<Foundation.Recovery.Crypto.IFieldEncryptor>(),
            decryptor, capabilityProvider, statusStore, context,
            Array.Empty<IIntegrationSchemaProvider>(),
            new[] { validator });

        var result = await provider.ValidateProviderAsync(IntegrationCategory.TransactionalEmail, context);

        // Must be Unknown + "decrypt-failed" — NOT a validator call that could return Valid
        Assert.Equal(ProviderValidationStatus.Unknown, result.Status);
        Assert.Equal("decrypt-failed", result.ErrorCode);

        // Validator must NOT have been called
        await validator.DidNotReceive().ValidateAsync(
            Arg.Any<IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>>>(),
            Arg.Any<IReadOnlyDictionary<string, System.Text.Json.Nodes.JsonNode>>(),
            Arg.Any<CancellationToken>());
    }
}
