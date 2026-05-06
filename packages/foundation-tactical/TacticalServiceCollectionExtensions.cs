using System;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// DI registration for the foundation-tier Tactical substrate
/// (ADR 0081). Per cohort <c>AddSunfishX()</c> convention. Phase 1
/// binds <see cref="TacticalOptions"/> only; concrete
/// <see cref="ITacticalRuleEngine"/> + <see cref="IAlertRouter"/> +
/// <see cref="IThreatTriggerService"/> +
/// <see cref="ITacticalDataProvider"/> +
/// <see cref="ITacticalCommandService"/> implementations land in
/// Phase 2.
/// </summary>
public static class TacticalServiceCollectionExtensions
{
    /// <summary>
    /// Register the Tactical substrate. Phase 1 ships interface
    /// surface + options binding; hosts MUST register concrete
    /// implementations via Phase 2 or their own DI composition before
    /// invoking the surfaces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>§8.1 startup ShipAction registration check + §8.3 rule-name
    /// allowlist verification</b> live with the Phase 2
    /// <c>DefaultTacticalRuleEngine</c> + <c>DefaultAlertRouter</c>
    /// registration — they require concrete provider + rule
    /// instances to validate against. Phase 1 does not register an
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/> for
    /// these checks; Phase 2 wires them in alongside the data
    /// provider.
    /// </para>
    /// <para>
    /// <b>§4.1 system-principal authority check (Phase 2 wiring
    /// invariant):</b> Phase 2 <c>IPermissionResolver</c> registration
    /// MUST NOT grant
    /// <c>Sunfish.Foundation.Ship.Common.ShipAction.IssueEmergencyStandingOrder</c>
    /// to ANY value of the v1 <c>ShipRole</c> enum (Captain / XO /
    /// EngineerOfficer / Navigator / TacticalOfficer /
    /// DivisionOfficer / IDC / Scribe / SUPPO / OOD / EOOW). The
    /// action is granted exclusively at DI bootstrap to a
    /// programmatically-constructed system principal whose
    /// <see cref="Sunfish.Foundation.Crypto.PrincipalId"/> Phase 2's
    /// <c>DefaultThreatTriggerService</c> verifies against
    /// <see cref="ISystemPrincipalProvider.GetSystemPrincipalAsync"/>
    /// immediately before issuance — a
    /// <see cref="Sunfish.Foundation.Crypto.PrincipalId"/> equality
    /// check, not a role lookup. <c>blocks-tactical</c> startup MUST
    /// fail fast on misconfiguration.
    /// </para>
    /// </remarks>
    /// <param name="services">DI container.</param>
    /// <param name="configure">
    /// Optional configuration callback invoked against a fresh
    /// options instance seeded with canonical defaults.
    /// </param>
    public static IServiceCollection AddSunfishTactical(
        this IServiceCollection services,
        Action<TacticalOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<TacticalOptions>().Configure(opts =>
        {
            // Defaults already applied via property initializers; the
            // configure callback (when supplied) overrides individual
            // fields.
            configure?.Invoke(opts);
        });

        return services;
    }
}
