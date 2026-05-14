using System;
using System.Collections.Generic;
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

/// <summary>
/// Verifies ADR 0067 §7.1 encrypt-before-issue ordering invariant.
/// </summary>
public sealed class SensitiveCredentialOrderingTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");
    private static readonly PrincipalId TestPrincipal = PrincipalId.FromBytes(new byte[32]);
    private static readonly Signature TestSignature = Signature.FromBytes(new byte[64]);

    [Fact]
    public async Task SensitiveCredential_IsEncryptedBeforeStandingOrder()
    {
        var callOrder = new List<string>();

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.EncryptAsync(Arg.Any<System.ReadOnlyMemory<byte>>(), Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("encrypt");
                return Task.FromResult(new EncryptedField(
                    new System.ReadOnlyMemory<byte>(new byte[16]),
                    new System.ReadOnlyMemory<byte>(new byte[12]),
                    1));
            });

        var issuer = Substitute.For<IStandingOrderIssuer>();
        issuer.IssueAsync(Arg.Any<StandingOrderDraft>(), Arg.Any<ActorId>(), Arg.Any<IAuditTrail>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("issue");
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

        var decryptor = Substitute.For<IFieldDecryptor>();
        var capabilityProvider = Substitute.For<IDecryptCapabilityProvider>();
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
            encryptor, decryptor, capabilityProvider, statusStore, context,
            Array.Empty<IIntegrationSchemaProvider>(),
            Array.Empty<IIntegrationProviderValidator>());

        var ctx = Substitute.For<IIntegrationAtlasContext>();
        ctx.CurrentTenantId.Returns(TenantA);
        ctx.CurrentActorId.Returns(ActorA);

        await provider.IssueSensitiveCredentialAsync(
            IntegrationCategory.TransactionalEmail, "sendgrid", "apiKey",
            new System.ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }), ctx);

        Assert.Equal(2, callOrder.Count);
        Assert.Equal("encrypt", callOrder[0]);
        Assert.Equal("issue", callOrder[1]);
    }
}
