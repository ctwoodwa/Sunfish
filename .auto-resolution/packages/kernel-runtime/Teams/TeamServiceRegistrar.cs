using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Delegate that populates a fresh <see cref="IServiceCollection"/> with the
/// services that should be resolved <em>per team</em>. Invoked once per team
/// at the moment that team's <see cref="TeamContext"/> is first materialized.
/// </summary>
/// <remarks>
/// <para>
/// This is the integration seam between Wave 6.1 (this file — scaffolded shape)
/// and Wave 6.3 (real per-team service wiring: team-scoped <c>IGossipDaemon</c>,
/// <c>ILeaseCoordinator</c>, <c>IEventLog</c>, <c>IEncryptedStore</c>,
/// <c>IQuarantineQueue</c>, <c>IBucketRegistry</c>, plus a per-team <c>IPluginRegistry</c>
/// bound to that team's plugin set).
/// </para>
/// <para>
/// Wave 6.1 ships a no-op default (see
/// <c>TeamContextFactory.DefaultRegistrar</c>) so the factory and the
/// <see cref="IActiveTeamAccessor"/> can be exercised end-to-end without Wave 6.3
/// being in place. Consumers in Wave 6.3 will replace the default with a real
/// registrar via <c>AddSunfishMultiTeam(registrar)</c>.
/// </para>
/// </remarks>
/// <param name="services">The fresh <see cref="IServiceCollection"/> to populate.
/// Services registered here will be available through
/// <see cref="TeamContext.Services"/> for that team only.</param>
/// <param name="teamId">The team whose scope is being constructed. Registrars that
/// need per-team paths (SQLCipher DB filename, event-log directory, keystore
/// prefix) branch on this.</param>
public delegate void TeamServiceRegistrar(IServiceCollection services, TeamId teamId);
