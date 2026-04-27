using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.Extensions;
using Sunfish.Kernel.Runtime.DependencyInjection;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.DependencyInjection;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.DependencyInjection;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;
using Sunfish.Providers.Bootstrap.Extensions;
using Microsoft.Maui.Storage;

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

		// Plan 2 Task 4.2 — Sunfish localization, Anchor side. Mirrors Bridge's
		// composition root (accelerators/bridge/Sunfish.Bridge/Program.cs) but
		// without UseRequestLocalization (no HTTP) and without
		// SunfishProblemDetailsFactory (no ProblemDetails). MAUI sets
		// CultureInfo.CurrentUICulture from the device's preferred UI language at
		// startup; IStringLocalizer<SharedResource> picks up the satellite RESX
		// files under Resources/Localization/ via the standard satellite-assembly
		// probe path.
		//
		// 12-locale roster matches Bridge per the Global-First UX spec
		// (en-US, es-419, pt-BR, fr, de, ja, zh-Hans, ar-SA, hi, he-IL, fa-IR, ko).
		// Locale satellites scaffold incrementally; locale-completeness-check tool
		// (tooling/locale-completeness-check/check.mjs) reports per-bundle per-locale
		// percentages and gates against per-tier floors once the first complete-tier
		// locale clears 95% on at least one bundle.
		builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

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
		// byte-for-byte consistent on their team-scoped key derivation. Wave
		// 6.7.A swapped the zero-seed stub for a keystore-backed
		// IRootSeedProvider; each Anchor install now derives cryptographically
		// independent keys while same-machine relaunches reuse the same seed.
		var dataDirectory = FileSystem.AppDataDirectory;
		var keysDirectory = Path.Combine(dataDirectory, "keys");

		byte[] rootSeed;
		using (var bootstrapServices = new ServiceCollection()
			.AddSunfishRootSeedProvider(keystoreStorageDirectory: keysDirectory)
			.BuildServiceProvider())
		{
			var seedProvider = bootstrapServices.GetRequiredService<IRootSeedProvider>();
			// Synchronous block at MAUI composition time. The MAUI startup thread
			// has no SynchronizationContext at this point; blocking here is safe
			// and unambiguous vs. making CreateMauiApp itself async.
			rootSeed = seedProvider.GetRootSeedAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult().ToArray();
		}

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
		// Wave 6.7.A — keystore-backed root-seed provider for the app DI container.
		builder.Services
			.AddSunfishKernelRuntime()
			.AddSunfishKernelSecurity()
			.AddSunfishRootSeedProvider(keystoreStorageDirectory: keysDirectory)
			.AddSunfishDefaultTeamRegistrar(
				dataDirectory: dataDirectory,
				rootIdentity: rootIdentity,
				subkeyDerivation: subkeyDerivation,
				sqlCipherKeyDerivation: sqlCipherKeyDerivation)
			.AddSunfishTeamStoreActivator(rootSeed);

		// Wave 6.7 — v1→v2 migration runs BEFORE the bootstrap hosted service
		// so the default-team materialization sees the v2 layout. Hosted
		// services start in registration order, and the migration's
		// StartAsync runs the move inline before returning.
		//
		// The legacy team id is derived deterministically from the first 16
		// bytes of the install's root Ed25519 public key — identical to the
		// NodeId convention used elsewhere in this composition root. Because
		// the root seed is install-scoped and deterministic, the same
		// machine produces the same legacy team id across relaunches; the
		// migration persists the value in its .migration-v2 marker so
		// subsequent launches never re-derive it even if the seed source
		// changes.
		var legacyTeamIdBytes = new byte[16];
		Buffer.BlockCopy(rootPublicKey, 0, legacyTeamIdBytes, 0, 16);
		var legacyTeamId = new TeamId(new Guid(legacyTeamIdBytes));

		builder.Services.AddSingleton<AnchorV1MigrationService>(sp =>
			new AnchorV1MigrationService(
				dataDirectory: dataDirectory,
				legacyTeamIdProvider: () => legacyTeamId,
				logger: sp.GetRequiredService<ILogger<AnchorV1MigrationService>>()));
		builder.Services.AddHostedService(sp =>
			sp.GetRequiredService<AnchorV1MigrationService>());

		// AddHostedService registers the bootstrap service in DI, but MAUI's
		// MauiApp does NOT implement IHost — it exposes only IServiceProvider
		// (verified against MAUI 10 preview docs). The lifecycle is pumped
		// manually from App.xaml.cs via Window.Created / Window.Destroying,
		// through Services/MauiHostedServiceLifetime. This preserves the same
		// AddHostedService<T>() composition pattern used by
		// apps/local-node-host (which does get an IHost and thus auto-pump).
		builder.Services.AddHostedService<AnchorBootstrapHostedService>();

		// Phase 1 G1 — Anchor sync (paper §6.1, ADR 0029, sync-daemon-protocol spec).
		//
		// Composition order:
		//   1. ISyncDaemonTransport BEFORE AddSunfishKernelSync — AddSunfishKernelSync
		//      uses TryAddSingleton, so a pre-registered transport with the
		//      app's listen endpoint wins over the outbound-only default.
		//      Anchor uses a Windows named pipe per ADR 0044 (Phase 1 Win64-only).
		//      Convention: pipe name "sunfish-anchor-{nodeId-prefix}" so multi-install
		//      coexistence on one machine doesn't collide.
		//   2. INodeIdentityProvider BEFORE AddSunfishKernelSync — same TryAddSingleton
		//      discipline. Anchor's rootIdentity (line 88) is the wire identity for
		//      HELLO + GOSSIP_PING signatures; the in-memory provider wraps it.
		//   3. AddSunfishKernelSync — registers IGossipDaemon + VectorClock + the
		//      fallback signer. Idempotent on its own services.
		//   4. AddMdnsPeerDiscovery — registers IPeerDiscovery for tier-1 LAN
		//      (paper §6.1). WAN ManagedRelayPeerDiscovery is Phase 1 G4.
		//   5. AddHostedService<AnchorSyncHostedService> AFTER the bootstrap hosted
		//      service so the default-team identity + active-team accessor are
		//      materialized before the daemon starts advertising on the LAN.
		var syncPipeName = $"sunfish-anchor-{rootIdentity.NodeId[..8]}";
		builder.Services.TryAddSingleton<ISyncDaemonTransport>(_ =>
			new UnixSocketSyncDaemonTransport(syncPipeName));
		builder.Services.TryAddSingleton<INodeIdentityProvider>(_ =>
			new InMemoryNodeIdentityProvider(rootIdentity));
		builder.Services.AddSunfishKernelSync();
		builder.Services.AddMdnsPeerDiscovery();
		builder.Services.AddHostedService<AnchorSyncHostedService>();

		// Anchor-specific session state + onboarding service.
		builder.Services.AddSingleton<AnchorSessionService>();

		// Wave 6.8 — QrOnboardingService is wired with the multi-team Wave 6.8
		// dependencies (ITeamContextFactory, ITeamStoreActivator,
		// ITeamSubkeyDerivation, root NodeIdentity) so the team-switcher page
		// can drive the join-additional-team flow. Subkey derivation + root
		// identity are closures over the composition-time values above so the
		// service matches the same byte-for-byte derivation the default team
		// registrar performs (keeps Wave 6.2 + Wave 6.3.E semantics aligned).
		builder.Services.AddSingleton<QrOnboardingService>(sp =>
			new QrOnboardingService(
				signer: sp.GetRequiredService<IEd25519Signer>(),
				activeTeam: sp.GetRequiredService<IActiveTeamAccessor>(),
				verifier: sp.GetRequiredService<IAttestationVerifier>(),
				issuer: sp.GetRequiredService<IAttestationIssuer>(),
				factory: sp.GetRequiredService<ITeamContextFactory>(),
				storeActivator: sp.GetRequiredService<ITeamStoreActivator>(),
				subkeyDerivation: subkeyDerivation,
				rootIdentity: rootIdentity));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
