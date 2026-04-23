using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }
}
