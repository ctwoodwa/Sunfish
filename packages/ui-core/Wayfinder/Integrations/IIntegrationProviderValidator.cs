using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Per-provider validator per ADR 0067 §6.2. Adapter packages register
/// concrete implementations via DI; only
/// <c>DefaultIntegrationAtlasProvider</c> consumes this interface
/// (Phase 2). Application code MUST NOT take a direct dependency.
/// </summary>
/// <remarks>
/// <para>
/// Sensitive credentials are passed as
/// <see cref="System.ReadOnlyMemory{T}"/> of bytes (already decrypted
/// at the call boundary by <c>IFieldDecryptor</c>); non-sensitive
/// fields are passed as <see cref="JsonNode"/>. Implementations MUST
/// NOT log, persist, or copy the sensitive bytes outside the validator
/// call scope per §Trust.
/// </para>
/// <para>
/// Cycle-safe: takes only primitive collections + <see cref="JsonNode"/>;
/// no foreign-package types. <c>IDecryptCapability</c> is not required —
/// the caller (<c>DefaultIntegrationAtlasProvider</c>) acquires the
/// capability and passes the already-decrypted bytes here.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IIntegrationProviderValidator
{
    /// <summary>The category this validator supports.</summary>
    IntegrationCategory SupportedCategory { get; }

    /// <summary>
    /// The provider identifier this validator supports — matches
    /// <see cref="IntegrationProviderSchema.ProviderId"/>.
    /// </summary>
    string SupportedProvider { get; }

    /// <summary>
    /// Validate the supplied credentials against the live provider.
    /// Implementations SHOULD complete in &lt;5s and MUST surface
    /// network failures as <see cref="ProviderValidationStatus.Unreachable"/>
    /// rather than throwing.
    /// </summary>
    Task<IntegrationValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>> sensitiveCredentials,
        IReadOnlyDictionary<string, JsonNode> nonSensitiveCredentials,
        CancellationToken ct);
}
