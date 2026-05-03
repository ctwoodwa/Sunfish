using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Macaroons;
using Sunfish.Foundation.PolicyEvaluator;

namespace Sunfish.Foundation.Extensions;

/// <summary>
/// DI extensions that register the Sunfish Phase B decentralization primitives —
/// Ed25519 verifier, capability graph, ReBAC policy evaluator, and (gated) macaroon
/// issuer / verifier backed by in-memory dev key material — onto the foundation
/// <see cref="SunfishBuilder"/>.
/// </summary>
public static class SunfishDecentralizationExtensions
{
    /// <summary>
    /// Registers Sunfish decentralization primitives — crypto, capability graph,
    /// ReBAC policy evaluator — in the DI container. Returns the builder for chaining.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registrations (always):
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="IOperationVerifier"/> → <see cref="Ed25519Verifier"/> (singleton).</description></item>
    ///   <item><description><see cref="ICapabilityGraph"/> → <see cref="InMemoryCapabilityGraph"/> (singleton).</description></item>
    ///   <item><description><see cref="PolicyModel"/> built from the caller's <see cref="DecentralizationOptions.PolicyModel"/>
    ///   callback (singleton).</description></item>
    ///   <item><description><see cref="IRelationTupleStore"/> → <see cref="InMemoryRelationTupleStore"/> (singleton).</description></item>
    ///   <item><description><see cref="IPermissionEvaluator"/> → <see cref="ReBACPolicyEvaluator"/> (singleton).</description></item>
    /// </list>
    /// <para>
    /// Registrations (only when <see cref="DecentralizationOptions.EnableDevKeyMaterial"/> is <c>true</c>):
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="DevKeyStore"/> (singleton).</description></item>
    ///   <item><description><see cref="IRootKeyStore"/> → <see cref="InMemoryRootKeyStore"/> (singleton).</description></item>
    ///   <item><description><see cref="IMacaroonIssuer"/> → <see cref="DefaultMacaroonIssuer"/> (singleton).</description></item>
    ///   <item><description><see cref="IMacaroonVerifier"/> → <see cref="DefaultMacaroonVerifier"/> (singleton).</description></item>
    ///   <item><description>A startup <see cref="IHostedService"/> that emits a
    ///     <see cref="LogLevel.Warning"/> so dev key material is visible in logs.</description></item>
    /// </list>
    /// <para>
    /// Production callers should leave <see cref="DecentralizationOptions.EnableDevKeyMaterial"/>
    /// as <c>false</c> and register their own <see cref="IRootKeyStore"/>,
    /// <see cref="IMacaroonIssuer"/>, and <see cref="IMacaroonVerifier"/> — backed by KMS / HSM /
    /// OS-keyring material — before using macaroons.
    /// </para>
    /// </remarks>
    public static SunfishBuilder AddSunfishDecentralization(
        this SunfishBuilder builder,
        Action<DecentralizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new DecentralizationOptions();
        configure?.Invoke(options);

        // Stateless verifier + graph (always).
        builder.Services.AddSingleton<IOperationVerifier, Ed25519Verifier>();
        builder.Services.AddSingleton<ICapabilityGraph, InMemoryCapabilityGraph>();

        // Policy evaluator (always — needs no key material). Build the model once here and
        // register as a singleton so the PolicyModel callback is invoked eagerly and the same
        // immutable model is shared by every resolution of IPermissionEvaluator.
        var modelBuilder = PolicyModel.Create();
        options.PolicyModel?.Invoke(modelBuilder);
        var model = modelBuilder.Build();
        builder.Services.AddSingleton(model);
        builder.Services.AddSingleton<IRelationTupleStore, InMemoryRelationTupleStore>();
        builder.Services.AddSingleton<IPermissionEvaluator, ReBACPolicyEvaluator>();

        // Dev-only key material + macaroon defaults — gated.
        if (options.EnableDevKeyMaterial)
        {
            builder.Services.AddSingleton<DevKeyStore>();
            builder.Services.AddSingleton<IRootKeyStore, InMemoryRootKeyStore>();
            builder.Services.AddSingleton<IMacaroonVerifier, DefaultMacaroonVerifier>();
            builder.Services.AddSingleton<IMacaroonIssuer, DefaultMacaroonIssuer>();
            builder.Services.AddSingleton<IHostedService, DevKeyMaterialWarningService>();
        }

        return builder;
    }

    /// <summary>
    /// Emits a <see cref="LogLevel.Warning"/> on startup when dev key material is active,
    /// so the non-production wiring is visible in logs / telemetry. Registered as an
    /// <see cref="IHostedService"/> so it runs once per process startup.
    /// </summary>
    internal sealed class DevKeyMaterialWarningService : IHostedService
    {
        private readonly ILogger<DevKeyMaterialWarningService> _logger;

        public DevKeyMaterialWarningService(ILogger<DevKeyMaterialWarningService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning(
                "DEV KEY MATERIAL ACTIVE: Sunfish decentralization is running with EnableDevKeyMaterial = true. " +
                "In-memory DevKeyStore and InMemoryRootKeyStore are registered. " +
                "DO NOT DEPLOY TO PRODUCTION. Replace with KMS / HSM / OS-keyring-backed IOperationSigner and IRootKeyStore before shipping.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
