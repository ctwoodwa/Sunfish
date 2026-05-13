using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Atlas integration-config provider per ADR 0067 §3 + §6. Read +
/// write surface for tenant-scoped third-party integration config
/// (payments, transactional/marketing email, messaging, mesh VPN,
/// captcha) — projects the latest committed Standing-Order log into
/// an <see cref="IntegrationAtlasView"/> and issues new Standing
/// Orders for provider changes, credential updates, validation
/// requests, and email-routing changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cycle-safe return type:</b> the hand-off cites
/// <c>Task&lt;StandingOrder&gt;</c>; <c>StandingOrder</c> lives in
/// <c>Sunfish.Foundation.Wayfinder</c>, which transitively
/// references <c>kernel-crdt</c> → <c>ui-core</c>. Adding the dep
/// would form a cycle. Per W#48 P1.5 cycle-break precedent
/// (<see cref="StandingOrderId"/> relocated to
/// <c>Sunfish.Foundation.Assets.Common</c>), every issuance method
/// returns <see cref="StandingOrderId"/> instead — the lightweight
/// identifier round-trips through the cycle-safe assets-common
/// package. Callers needing the full Standing-Order record fetch
/// it via the W#42 <c>IStandingOrderRepository</c> at the boundary.
/// </para>
/// <para>
/// <b>Side-effect-free read path:</b>
/// <c>GetAtlasViewAsync</c> inherits the
/// <see cref="IAtlasProvider{TView}"/> projection-only contract —
/// no mutations, no audit emission, no Standing-Order issuance.
/// Mutations flow through the <c>IssueXxxAsync</c> methods, which
/// emit ADR 0067 §8 audit events + issue Standing Orders.
/// </para>
/// <para>
/// <b>Sensitive-credential isolation:</b>
/// <see cref="IssueSensitiveCredentialAsync"/> takes a
/// <see cref="ReadOnlyMemory{T}"/> of plaintext bytes; Phase 2
/// <c>DefaultIntegrationAtlasProvider</c> calls
/// <c>IFieldEncryptor.EncryptAsync</c> BEFORE
/// <c>IStandingOrderIssuer.IssueAsync</c> per §7.1, and zeroes the
/// plaintext buffer in <c>finally</c>. The contract surface admits
/// no method that returns plaintext credential bytes —
/// <c>ContractSurfaceTests.NoMethodReturnsDecryptedBytes</c>
/// reflects this invariant.
/// </para>
/// <para>
/// <b>Audit events</b> emitted by Phase 2 implementations (per ADR
/// 0067 §8): <c>IntegrationProviderChanged</c> /
/// <c>IntegrationCredentialUpdated</c> /
/// <c>IntegrationValidationSucceeded</c> /
/// <c>IntegrationValidationFailed</c> — all in
/// <c>Sunfish.Kernel.Audit.AuditEventType</c>. Audit
/// payloads MUST NOT contain credential values per the §8
/// redaction rule.
/// </para>
/// </remarks>
public interface IIntegrationAtlasProvider : IAtlasProvider<IntegrationAtlasView>
{
    /// <summary>
    /// Snapshot of all schemas currently registered with this
    /// provider via <see cref="IIntegrationSchemaProvider"/>
    /// adapters. Used by the Atlas integration-config UI to render
    /// the per-category provider picker.
    /// </summary>
    IReadOnlyList<IntegrationProviderSchema> GetSchemas();

    /// <summary>
    /// Issue a Standing Order activating
    /// <paramref name="providerId"/> as the active provider for
    /// <paramref name="category"/>. Per §7.1 rotation-non-destruction:
    /// the prior provider's credentials remain in the Standing-Order
    /// log; only the active-pointer projection updates.
    /// </summary>
    Task<StandingOrderId> IssueProviderChangeAsync(
        IntegrationCategory category,
        string providerId,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default);

    /// <summary>
    /// Issue a Standing Order updating a sensitive credential field
    /// (API key, OAuth token, webhook secret) for the
    /// <paramref name="providerId"/> in <paramref name="category"/>.
    /// Phase 2 implementations encrypt
    /// <paramref name="plaintextBytes"/> via
    /// <c>IFieldEncryptor.EncryptAsync</c> BEFORE issuing the
    /// Standing Order; callers MUST zero the
    /// <paramref name="plaintextBytes"/> buffer themselves after
    /// the call returns. Audit payloads NEVER contain the credential
    /// value per §8.
    /// </summary>
    Task<StandingOrderId> IssueSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerId,
        string credentialKey,
        ReadOnlyMemory<byte> plaintextBytes,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default);

    /// <summary>
    /// Issue a Standing Order updating a non-sensitive credential
    /// field (URL, public id, region selector) for the
    /// <paramref name="providerId"/> in <paramref name="category"/>.
    /// Stored as JSON; not encrypted. Suitable for public + reference
    /// values where a downstream UI may legitimately read the
    /// stored value back.
    /// </summary>
    Task<StandingOrderId> IssueNonSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerId,
        string credentialKey,
        JsonNode value,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default);

    /// <summary>
    /// Validate the current credential set for
    /// <paramref name="category"/>'s active provider against the
    /// registered <see cref="IIntegrationProviderValidator"/>.
    /// Capability flow per §5.3.1:
    /// <list type="number">
    /// <item><description>Acquire a short-lived
    /// <see cref="Sunfish.Foundation.Crypto.IDecryptCapability"/>
    /// via
    /// <see cref="Sunfish.Foundation.Crypto.IDecryptCapabilityProvider.AcquireAsync"/>
    /// with purpose
    /// <see cref="IntegrationCapabilityPurposes.IntegrationValidation"/>.</description></item>
    /// <item><description>If <c>null</c> (host policy denial / no
    /// DEK / unknown tenant): return
    /// <see cref="ProviderValidationStatus.Unknown"/> +
    /// <c>ErrorCode = "no-decrypt-capability"</c>. NEVER throw.</description></item>
    /// <item><description>Otherwise: decrypt sensitive fields,
    /// invoke the validator, persist the result via
    /// <see cref="IValidationStatusStore.UpdateAsync"/>, zero the
    /// plaintext buffers in <c>finally</c>, and return the result.</description></item>
    /// </list>
    /// </summary>
    Task<IntegrationValidationResult> ValidateProviderAsync(
        IntegrationCategory category,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default);

    /// <summary>
    /// Issue a Standing Order updating the tenant-scoped email
    /// routing record (transactional + marketing senders). Phase 2
    /// implementations may emit a single Standing Order or two
    /// (one per sender) depending on which fields changed; callers
    /// observe a single returned <see cref="StandingOrderId"/>
    /// referencing the most-recent issuance.
    /// </summary>
    Task<StandingOrderId> IssueRoutingAsync(
        IntegrationEmailRouting routing,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default);
}
