using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Security.DependencyInjection;

/// <summary>
/// Registers the kernel-security surface (Ed25519/X25519 primitives, attestation
/// issuer+verifier, role-key manager) in a DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Sunfish.Kernel.Security services as singletons.
    /// Callers must also register an <see cref="IKeystore"/> separately — the
    /// platform-appropriate one can be obtained via
    /// <c>IKeystore keystore = Keystore.CreateForCurrentPlatform();</c>.
    /// </summary>
    public static IServiceCollection AddSunfishKernelSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEd25519Signer, Ed25519Signer>();
        services.AddSingleton<IX25519KeyAgreement, X25519KeyAgreement>();
        services.AddSingleton<IAttestationIssuer, AttestationIssuer>();
        services.AddSingleton<IAttestationVerifier, AttestationVerifier>();
        services.AddSingleton<IRoleKeyManager, RoleKeyManager>();
        services.AddSingleton<ITeamSubkeyDerivation, TeamSubkeyDerivation>();
        services.AddSingleton<ISqlCipherKeyDerivation, SqlCipherKeyDerivation>();

        return services;
    }

    /// <summary>
    /// Registers the keystore-backed <see cref="IRootSeedProvider"/> for this
    /// install. Ensures an <see cref="IKeystore"/> is registered (via
    /// <see cref="Keystore.CreateForCurrentPlatform(string?)"/>) and registers
    /// <see cref="KeystoreRootSeedProvider"/> as the
    /// <see cref="IRootSeedProvider"/> implementation. Both registrations are
    /// singleton and use <c>TryAdd</c>, so tests (or bridge/anchor composition
    /// roots) can override either service by registering their own
    /// <see cref="IKeystore"/> or <see cref="IRootSeedProvider"/> before
    /// calling this extension (e.g., <see cref="InMemoryKeystore"/> for
    /// deterministic per-test seeds).
    /// </summary>
    /// <param name="services">The DI container to register into.</param>
    /// <param name="keystoreStorageDirectory">
    /// Optional override for the platform keystore's on-disk storage
    /// directory. On Windows this backs the DPAPI ciphertext file; defaults to
    /// <c>%LOCALAPPDATA%/Sunfish/keys</c>. macOS and Linux stubs currently
    /// throw <see cref="PlatformNotSupportedException"/> on first use (Wave-2
    /// follow-up).
    /// </param>
    public static IServiceCollection AddSunfishRootSeedProvider(
        this IServiceCollection services,
        string? keystoreStorageDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IKeystore>(_ =>
            Keystore.CreateForCurrentPlatform(keystoreStorageDirectory));
        services.TryAddSingleton<IRootSeedProvider, KeystoreRootSeedProvider>();

        return services;
    }
}
