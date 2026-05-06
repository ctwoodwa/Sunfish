using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Crypto;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// DI registration for the Atlas integration-config substrate
/// (ADR 0067). Phase 1b binds the cycle-safe contract surface +
/// in-memory <see cref="IValidationStatusStore"/> only — concrete
/// <see cref="IIntegrationAtlasProvider"/> implementations
/// (<c>DefaultIntegrationAtlasProvider</c>) live in
/// <c>blocks-integrations</c> per the W#48 Phase 2 cycle-resolution
/// addendum and are wired by their own
/// <c>AddBlocksIntegrations()</c> extension.
/// </summary>
public static class IntegrationAtlasServiceCollectionExtensions
{
    /// <summary>
    /// Register the Atlas integration-config contract surface.
    /// Idempotent on <see cref="IValidationStatusStore"/> — calls
    /// after the first leave the existing registration intact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Required prerequisite — recovery wiring:</b> hosts MUST
    /// call <c>AddSunfishRecoveryCoordinator()</c> (in
    /// <c>foundation-recovery</c>) BEFORE this extension. The
    /// recovery wiring registers
    /// <see cref="IDecryptCapabilityProvider"/>, which Phase 2's
    /// <c>DefaultIntegrationAtlasProvider</c> requires for the
    /// <see cref="IIntegrationAtlasProvider.ValidateProviderAsync"/>
    /// capability flow per ADR 0067 §5.3.1. This extension throws
    /// <see cref="InvalidOperationException"/> when called against
    /// a service collection that does not yet have
    /// <see cref="IDecryptCapabilityProvider"/> registered — the
    /// fail-fast guard prevents silent capability-flow degradation.
    /// </para>
    /// <para>
    /// <b>Additional prerequisite — tenant-key wiring:</b> the
    /// reference <see cref="IDecryptCapabilityProvider"/>
    /// implementation
    /// (<c>Sunfish.Foundation.Recovery.Crypto.TenantKeyDecryptCapabilityProvider</c>)
    /// transitively depends on
    /// <c>Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider</c>.
    /// Hosts MUST register an <c>ITenantKeyProvider</c> implementation
    /// (e.g., the recovery package's HKDF-backed default) before
    /// resolving <see cref="IIntegrationAtlasProvider"/> at
    /// runtime. The Phase 1b guard cannot check for
    /// <c>ITenantKeyProvider</c> directly without taking a
    /// <c>foundation-recovery</c> ProjectReference (which would form
    /// a cycle: <c>ui-core → foundation-recovery → kernel-security →
    /// ui-core</c>). DI resolution at <c>BuildServiceProvider()</c>
    /// time surfaces the missing registration as a standard
    /// "no service for ITenantKeyProvider" exception pointing at
    /// <c>TenantKeyDecryptCapabilityProvider</c>'s constructor.
    /// </para>
    /// <para>
    /// <b>What this extension does NOT register:</b>
    /// <list type="bullet">
    /// <item><description><see cref="IIntegrationAtlasProvider"/>
    /// — concrete implementations live in
    /// <c>blocks-integrations</c> per the Phase 2 cycle-resolution
    /// addendum.</description></item>
    /// <item><description><c>IIntegrationAtlasContext</c> — the
    /// host registers this (Bridge: scoped via
    /// <c>HttpContext</c>; Anchor: singleton via local-node
    /// identity).</description></item>
    /// <item><description>Schema providers + validators — adapter
    /// packages (<c>providers-stripe</c>,
    /// <c>providers-sendgrid</c>, etc.) register their own
    /// <see cref="IIntegrationSchemaProvider"/> /
    /// <see cref="IIntegrationProviderValidator"/> instances via
    /// per-provider extensions.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Phase 2 wiring (preview):</b>
    /// <c>AddBlocksIntegrations(this IServiceCollection services)</c>
    /// in the Phase 2 <c>blocks-integrations</c> package will
    /// register <c>DefaultIntegrationAtlasProvider</c> as a
    /// singleton, enforce the
    /// <c>(SupportedCategory, SupportedProvider)</c> uniqueness
    /// invariant on validators (§6.2.1), and wire the
    /// <c>SUNFISH_INTEGRATION_AUDIT001</c> typed-payload
    /// requirement.
    /// </para>
    /// </remarks>
    /// <param name="services">DI container.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="IDecryptCapabilityProvider"/> is not
    /// registered. Call <c>AddSunfishRecoveryCoordinator()</c>
    /// first.
    /// </exception>
    public static IServiceCollection AddSunfishIntegrationAtlas(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(d => d.ServiceType == typeof(IDecryptCapabilityProvider)))
        {
            throw new InvalidOperationException(
                "AddSunfishRecoveryCoordinator() must be called before "
                + "AddSunfishIntegrationAtlas(). IDecryptCapabilityProvider is "
                + "required by Phase 2's DefaultIntegrationAtlasProvider for the "
                + "ADR 0067 §5.3.1 capability-acquisition flow.");
        }

        services.TryAddSingleton<IValidationStatusStore, InMemoryValidationStatusStore>();

        return services;
    }
}
