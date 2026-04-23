using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.Extensions;
using Sunfish.Kernel.Runtime.DependencyInjection;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.DependencyInjection;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Providers.Bootstrap.Extensions;

namespace Sunfish.Anchor;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Foundation base (ISunfishThemeService, ISunfishNotificationService, ...)
		// is required by the SunfishComponentBase infrastructure that the LocalFirst
		// components (Wave 3.1 + 3.2) inherit from. Bootstrap provider supplies
		// ISunfishCssProvider / ISunfishIconProvider / ISunfishJsInterop — the
		// LocalFirst components don't call any CSS methods but SunfishComponentBase
		// [Inject]s them, so DI must be able to resolve.
		builder.Services.AddSunfish()
			.AddSunfishBootstrap();

		// Wave 6.3.F — bind Anchor shell to TeamContext per ADR 0032.
		//
		// Anchor today owns a single active team at a time (it's a client shell,
		// not a server); the per-team factory still mediates access so the
		// switcher (Wave 6.6) and join-additional-team flow (Wave 6.8) can
		// extend the surface without touching service contracts.
		//
		// Root seed + identity + KDFs mirror local-node-host's composition root
		// (apps/local-node-host/Program.cs) so the two composition roots stay
		// byte-for-byte consistent on their team-scoped key derivation.
		var rootSeed = AnchorRootSeedReader.Read();
		var dataDirectory = AnchorRootSeedReader.GetDefaultDataDirectory();

		var signer = new Ed25519Signer();
		var (rootPublicKey, rootPrivateKey) = signer.GenerateFromSeed(rootSeed);
		var rootIdentity = new NodeIdentity(
			NodeId: Convert.ToHexString(rootPublicKey.AsSpan(0, 16)).ToLowerInvariant(),
			PublicKey: rootPublicKey,
			PrivateKey: rootPrivateKey);

		var subkeyDerivation = new TeamSubkeyDerivation(signer);
		var sqlCipherKeyDerivation = new SqlCipherKeyDerivation();

		// Paper §11.3 — Ed25519 signer, attestation issuer/verifier, role-key manager.
		// Paper §5.1 — node host + plugin registry.
		// Wave 6.3.E.1 — per-team service registrar + per-team store activator.
		// Wave 6.3.F — Anchor's single-team bootstrap hosted service.
		builder.Services
			.AddSunfishKernelRuntime()
			.AddSunfishKernelSecurity()
			.AddSunfishDefaultTeamRegistrar(
				dataDirectory: dataDirectory,
				rootIdentity: rootIdentity,
				subkeyDerivation: subkeyDerivation,
				sqlCipherKeyDerivation: sqlCipherKeyDerivation)
			.AddSunfishTeamStoreActivator(rootSeed);

		// AddHostedService registers the bootstrap service in DI, but MAUI's
		// MauiApp does NOT implement IHost — it exposes only IServiceProvider
		// (verified against MAUI 10 preview docs). The lifecycle is pumped
		// manually from App.xaml.cs via Window.Created / Window.Destroying,
		// through Services/MauiHostedServiceLifetime. This preserves the same
		// AddHostedService<T>() composition pattern used by
		// apps/local-node-host (which does get an IHost and thus auto-pump).
		builder.Services.AddHostedService<AnchorBootstrapHostedService>();

		// Anchor-specific session state + onboarding service.
		builder.Services.AddSingleton<AnchorSessionService>();
		builder.Services.AddSingleton<QrOnboardingService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
