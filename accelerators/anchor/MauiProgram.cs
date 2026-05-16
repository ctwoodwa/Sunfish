using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Anchor.Services;
using Sunfish.Blocks.CrewComms.DependencyInjection;
using Sunfish.Blocks.Properties.DependencyInjection;
using Sunfish.Blocks.PropertyEquipment.DependencyInjection;
using Sunfish.Blocks.Maintenance.DependencyInjection;
using Sunfish.Blocks.Leases.DependencyInjection;
using Sunfish.Blocks.Inspections.DependencyInjection;
using Sunfish.Foundation.Extensions;
using Sunfish.Foundation.IdentityAtlas;
using Sunfish.Foundation.Transport.DependencyInjection;
using Sunfish.UICore.Wayfinder;
using Sunfish.Kernel.Crdt;
using Sunfish.Kernel.Crdt.DependencyInjection;
using Sunfish.Kernel.Runtime.DependencyInjection;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.DependencyInjection;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Application;
using Sunfish.Kernel.Sync.DependencyInjection;
using Sunfish.Kernel.Sync.Discovery;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;
using Sunfish.Providers.Bootstrap.Extensions;
using Microsoft.Maui.Storage;
using Sunfish.Blocks.EngineRoom;
using Sunfish.Blocks.Integrations;
using Sunfish.Blocks.Quarterdeck;
using Sunfish.Blocks.ShipsOffice;
using Sunfish.Blocks.SickBay;
using Sunfish.Foundation.Quarterdeck;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Foundation.SickBay;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Providers.Mesh.Headscale.Integration;
using Sunfish.Providers.Recaptcha.Integration;
using Sunfish.UICore.Primitives;
using Sunfish.UICore.Wayfinder.Integrations;

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

		// Phase 1 G1 + G2 + G4 — Anchor sync (paper §6.1, §17.2; ADR 0029, ADR 0031;
		// sync-daemon-protocol spec).
		//
		// Composition order matters because most kernel-sync registrations use
		// TryAddSingleton, which keeps the FIRST registration. Anchor-specific
		// implementations have to be registered BEFORE AddSunfishKernelSync to
		// override the kernel's safe-default no-ops.
		//
		//   1. ISyncDaemonTransport — UnixSocketSyncDaemonTransport with a Windows
		//      named-pipe listen endpoint per ADR 0044 (Phase 1 Win64-only). Pipe
		//      name "sunfish-anchor-{nodeId-prefix}" so multi-install coexistence
		//      on one machine doesn't collide.
		//   2. INodeIdentityProvider wrapping rootIdentity (the Ed25519 keypair
		//      derived from the install's keystore-backed root seed at line 89).
		//   3. ICrdtEngine + the active CRDT document (Phase 1 single-document
		//      convention, id "default") — backs the AnchorCrdtDeltaBridge that
		//      kernel-sync's gossip daemon uses for DELTA_STREAM exchange.
		//   4. AnchorCrdtDeltaBridge — implements both IDeltaProducer + IDeltaSink
		//      from kernel-sync. Registered as singleton + as both interface keys
		//      so AddSunfishKernelSync's TryAddSingleton-noop defaults lose.
		//   5. AddSunfishKernelSync — registers IGossipDaemon + VectorClock + the
		//      fallback signer. Idempotent on its own services.
		//   6. AddMdnsPeerDiscovery — tier-1 LAN discovery (paper §6.1).
		//   7. AddManagedRelayPeerDiscovery — tier-3 WAN discovery (paper §17.2,
		//      ADR 0031 Zone-C). Empty RelayUrl is a no-op so LAN-only deployments
		//      register the source uniformly without producing peers. The Bridge
		//      relay URL + node id + public key come from Anchor settings UI in a
		//      future Stage 06 deliverable; for now read from the
		//      Sync:Discovery:Bridge configuration section.
		//   8. AddHostedService<AnchorSyncHostedService> AFTER the bootstrap
		//      hosted service so the default-team identity + active-team accessor
		//      are materialized before the daemon advertises and starts.
		var syncPipeName = $"sunfish-anchor-{rootIdentity.NodeId[..8]}";
		builder.Services.TryAddSingleton<ISyncDaemonTransport>(_ =>
			new UnixSocketSyncDaemonTransport(syncPipeName));
		builder.Services.TryAddSingleton<INodeIdentityProvider>(_ =>
			new InMemoryNodeIdentityProvider(rootIdentity));

		// CRDT engine (paper §2.2, ADR 0028) — YDotNet (Yjs/yrs) is the production
		// default; native binaries ship via the YDotNet.Native package and work on
		// Windows (Phase 1's only target per ADR 0044).
		builder.Services.AddSunfishCrdtEngine();
		builder.Services.TryAddSingleton<ICrdtDocument>(sp =>
			sp.GetRequiredService<ICrdtEngine>().CreateDocument("default"));
		builder.Services.TryAddSingleton<AnchorCrdtDeltaBridge>();
		builder.Services.TryAddSingleton<IDeltaProducer>(sp =>
			sp.GetRequiredService<AnchorCrdtDeltaBridge>());
		builder.Services.TryAddSingleton<IDeltaSink>(sp =>
			sp.GetRequiredService<AnchorCrdtDeltaBridge>());

		// W#29 Phase 1 — owner cockpit needs IPropertyRepository to back the
		// landing-page property selector. Wired here so the cockpit page can
		// resolve it from local-first DI; the InMemory repo is the only impl
		// today and is replaced once a SQLCipher-backed adapter ships.
		builder.Services.AddInMemoryProperties();
		// W#29 Phase 2 — property detail page reads equipment via
		// IEquipmentRepository. Same in-memory swap-out story as properties.
		builder.Services.AddInMemoryPropertyEquipment();
		// W#29 Phase 3 — work-orders pages read IMaintenanceService.
		builder.Services.AddInMemoryMaintenance();
		// W#29 Phase 5 — dashboard page joins leases + inspections per unit.
		builder.Services.AddInMemoryLeases();
		builder.Services.AddInMemoryInspections();

		// W#63 Phase 1 — ADR 0046 recovery substrate. RecoveryCoordinator
		// owns the multi-sig social-recovery state machine; consumed by
		// `RecoveryStatusPage.razor`. The other 4 hand-off pages
		// (TrusteeSetup / InitiateRecovery / ApproveRecovery / PaperKey)
		// are deferred pending an XO ruling on trustee-pubkey acquisition
		// + ephemeral-signing UX (cob-question 2026-05-16).
		// FixedDisputerValidator with an empty key set — Phase 1
		// placeholder; real validator queries the keystore for the owner's
		// other-device public keys once W#37 ITenantSecurityPolicy ships.
		builder.Services.AddSingleton<Sunfish.Foundation.Recovery.IRecoveryStateStore,
									  Sunfish.Foundation.Recovery.InMemoryRecoveryStateStore>();
		builder.Services.AddSingleton<Sunfish.Foundation.Recovery.IRecoveryClock,
									  Sunfish.Foundation.Recovery.SystemRecoveryClock>();
		builder.Services.AddSingleton<Sunfish.Foundation.Recovery.IDisputerValidator>(
			_ => new Sunfish.Foundation.Recovery.FixedDisputerValidator(System.Array.Empty<byte[]>()));
		builder.Services.AddSingleton<Sunfish.Foundation.Recovery.IRecoveryCoordinator,
									  Sunfish.Foundation.Recovery.RecoveryCoordinator>();
		// W#67 PR 4 — production binding for the ephemeral X25519 private
		// key the recovery flow needs. Registered BEFORE
		// AddAnchorRecoveryHost so its TryAddSingleton fallback to
		// InMemoryEphemeralRecoveryKeyStore is suppressed in production.
		builder.Services.AddSingleton<Sunfish.Anchor.Services.IEphemeralRecoveryKeyStore,
									  Sunfish.Anchor.Services.MauiSecureStorageEphemeralRecoveryKeyStore>();
		// W#63 Phase 2 — recovery-host pipeline. Registers
		// IRecoveryCompletionHandler + RecoveryGracePollingService that
		// polls EvaluateGracePeriodAsync on a 60s cadence (per XO ruling
		// 2026-05-16 §c — IRecoveryCoordinator has no event subscription;
		// events return synchronously from each coordinator method).
		builder.Services.AddAnchorRecoveryHost(builder.Configuration);
		// W#66 — ApproveRecoveryPage helper. Composes W#65 ISessionSignerAccessor
		// + IRecoveryCoordinator + INodeIdentityProvider into a single
		// SubmitAsync(request) call. Transient so each approval gets a fresh
		// signer (and to keep the page lightweight).
		builder.Services.TryAddSingleton(TimeProvider.System);
		builder.Services.AddTransient<RecoveryAttestationSubmitter>();
		// W#67 PR 5 — TrusteeSetupPage uses TrusteeSetupService to
		// designate trustees AND distribute the per-trustee seed
		// envelope in one step.
		builder.Services.AddTransient<TrusteeSetupService>();

		builder.Services.AddSunfishKernelSync();
		builder.Services.AddMdnsPeerDiscovery();
		builder.Services.AddManagedRelayPeerDiscovery(opts =>
		{
			// Sync:Discovery:Bridge:Url + RelayNodeId + RelayPublicKey populated by
			// the Stage 06 settings-UI deliverable. When absent or empty, the
			// discovery source is a no-op per ManagedRelayPeerDiscovery.StartAsync.
			opts.RelayUrl = builder.Configuration["Sync:Discovery:Bridge:Url"] ?? string.Empty;
			opts.RelayNodeId = builder.Configuration["Sync:Discovery:Bridge:NodeId"] ?? string.Empty;
			var hexKey = builder.Configuration["Sync:Discovery:Bridge:PublicKey"];
			if (!string.IsNullOrWhiteSpace(hexKey))
			{
				try { opts.RelayPublicKey = Convert.FromHexString(hexKey); }
				catch (FormatException) { /* invalid hex — leave empty, surfaces at HELLO verify */ }
			}
		});
		builder.Services.AddHostedService<AnchorSyncHostedService>();

		// W#59 Phase 1 — register the foundation-transport substrate so
		// NativeChannelProvider's ITransportSelector dependency resolves.
		// MUST run before AddSunfishCrewComms (which TryAdd-noops if
		// ITransportSelector is missing). LAN-mDNS demo uses Tier 1 only;
		// Tier 3 Bridge relay registration is required by DefaultTransportSelector
		// (always-tried fallback per ADR 0061) but is not exercised until
		// W#28 cross-relay flows wire a real relay URL — for the LAN demo
		// we register a placeholder URL that surfaces only on Tier-3 fall-
		// through (i.e., when mDNS itself fails to find a peer).
		var crewCommsRelayUrl = builder.Configuration["CrewComms:Relay:Url"]
			?? builder.Configuration["Sync:Discovery:Bridge:Url"];
		var crewCommsRelayUri = string.IsNullOrWhiteSpace(crewCommsRelayUrl)
			? new Uri("wss://placeholder.invalid/crew-comms")
			: new Uri(crewCommsRelayUrl);
		builder.Services.AddBridgeRelay(
			new Sunfish.Foundation.Transport.Relay.BridgeRelayOptions
			{
				RelayUrl = crewCommsRelayUri,
			});
		builder.Services.AddSunfishTransport();

		// W#45 P5 — register the native crew-comms provider.
		// W#59 Phase 2 — replace the empty in-memory roster with a
		// discovery-backed ICrewRoster (TeamMembershipCrewRoster) so the
		// LAN-mDNS demo surfaces same-team peers without manual seed code.
		// AddSingleton<ICrewRoster, TeamMembershipCrewRoster>() runs BEFORE
		// AddSunfishCrewComms's TryAddSingleton, so the adapter wins
		// resolution (the AddInMemory(empty) below is a no-op safeguard so
		// the builder lambda still satisfies its required-callback shape).
		// AddSunfishCrewComms uses TryAddSingleton<KeyPair> so callers may
		// pre-register a persistent KeyPair (loaded from secure storage) to
		// override the per-container fresh-keygen Phase-1 stub.
		builder.Services.AddSingleton<Sunfish.Foundation.Channels.ICrewRoster, TeamMembershipCrewRoster>();
		builder.Services.AddSunfishCrewComms(roster =>
			roster.AddInMemory(System.Array.Empty<Sunfish.Foundation.Channels.CrewMember>()));

		// W#59 Phase 3 — invitation bus + listener hosted service. The bus
		// is registered via two interfaces (read + writer surfaces) so UI
		// components inject the read-only ICrewCommsInvitationBus while
		// the listener hosted service injects the writer side; both share
		// the same singleton instance.
		builder.Services.AddSingleton<CrewCommsInvitationBus>();
		builder.Services.AddSingleton<ICrewCommsInvitationBus>(
			sp => sp.GetRequiredService<CrewCommsInvitationBus>());
		builder.Services.AddSingleton<ICrewCommsInvitationBusWriter>(
			sp => sp.GetRequiredService<CrewCommsInvitationBus>());
		builder.Services.AddHostedService<CrewCommsListenerHostedService>();

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

		// W#58 Phase 1a — Identity Atlas surface + stub backing-service defaults.
		// NullKeyStore / NullTrusteeRegistry / NullTeamRegistry are TryAdd so a
		// future wallet/keystore workstream can Replace with real implementations.
		builder.Services.AddSunfishIdentityAtlasDefaults();
		builder.Services.AddSingleton<IIdentityAtlasSurface, AnchorIdentityAtlasSurface>();

		// W#47 Phase 4 — consolidated via AddAnchorSystemRequirementsRenderer extension
		// (AnchorMauiServiceCollectionExtensions). Registers observer + renderer + surface.
		builder.Services.AddAnchorSystemRequirementsRenderer();

		// W#55 Phase 4 — Ship's Office aggregation surface (ADR 0083).
		// A3 council note: Anchor is single-operator; RequireSecondActorPublish=false
		// (the default) is correct here. Bridge sets it to true for regulated multi-
		// tenant posture. The explicit Configure call makes the intent auditable.
		builder.Services.AddSunfishShipsOfficeDefaults();
		builder.Services.Configure<ShipsOfficeOptions>(opts =>
		{
			opts.SnapshotPageSize = 200;
			opts.FallbackPollingInterval = TimeSpan.FromSeconds(60);
			opts.RequireSecondActorPublish = false;
		});

		// W#50 Phase 4 — Engine Room observability surface (ADR 0079).
		//
		// AddSunfishEngineRoomDefaults registers DefaultEngineRoomDataProvider +
		// DefaultEngineRoomCommandService.
		//
		// Ship-level permission seams (single-operator Anchor posture):
		//   IPermissionResolver → AnchorGrantAllPermissionResolver (grant-all stub;
		//     Bridge uses DefaultPermissionResolver with real role assignments).
		//   IActorPrincipalResolver → InMemoryActorPrincipalResolver with the
		//     install's root Ed25519 public key registered as the operator principal.
		//   IOodWatchService → AnchorNoOpOodWatchService (null EOOW; real OOD
		//     watch lifecycle is a separate foundation-wayfinder concern).
		builder.Services.AddSunfishEngineRoomDefaults();
		builder.Services.TryAddSingleton<IPermissionResolver, AnchorGrantAllPermissionResolver>();
		var actorResolver = new InMemoryActorPrincipalResolver();
		actorResolver.Register(
			new Sunfish.Foundation.Assets.Common.ActorId(rootIdentity.NodeId),
			new Sunfish.Foundation.Capabilities.Individual(
				Sunfish.Foundation.Crypto.PrincipalId.FromBytes(rootPublicKey)));
		builder.Services.TryAddSingleton<IActorPrincipalResolver>(actorResolver);
		builder.Services.TryAddSingleton<IOodWatchService, AnchorNoOpOodWatchService>();

		// W#51 Phase 4 — Quarterdeck entry-point surface (ADR 0080).
		//
		// AddSunfishQuarterdeck registers DefaultQuarterdeckDataProvider +
		// DefaultQuarterdeckCommandService. TryAdd semantics: the Engine Room
		// block already registered IPermissionResolver + IActorPrincipalResolver
		// + IOodWatchService via TryAdd above; this call reuses them.
		// No additional permission seam changes needed for Anchor single-operator
		// posture — AnchorGrantAllPermissionResolver grants ViewQuarterdeck to all.
		builder.Services.AddSunfishQuarterdeck();

		// W#54 Phase 4 — Sick Bay aggregation surface (ADR 0082).
		//
		// AddSunfishSickBayDefaults registers SickBayDataProvider +
		// DefaultFirstAidSurface + DefaultStretcherBearerPolicy +
		// SickBayCommandService + MedevacServiceImpl.
		// Anchor is single-operator; AnchorGrantAllPermissionResolver
		// (registered by Engine Room wiring above) grants ViewPharmacy
		// unconditionally — CanViewPharmacy=true in SickBayPage.razor.
		// RegisterNoopKeyRotationScheduler=true: the real W#32 scheduler
		// is not yet wired in Anchor; Noop is the safe default per ADR 0082-A1.4.
		builder.Services.AddSunfishSickBayDefaults(opts =>
		{
			opts.RegisterNoopKeyRotationScheduler = true;
			opts.RegisterPurpose("ssn", "Social Security Number");
			opts.RegisterPurpose("dob", "Date of Birth");
			opts.FallbackPollingInterval = TimeSpan.FromSeconds(60);
		});

		// W#48 Phase 4 — Integration Atlas config surface (ADR 0067).
		//
		// Anchor uses InMemoryIntegrationAtlasProvider (no encryption stack
		// wired; full DefaultIntegrationAtlasProvider requires IDecryptCapabilityProvider
		// from AddSunfishRecoveryCoordinator, which is not yet wired in Anchor).
		// Provider adapters (Headscale + reCAPTCHA) register IIntegrationSchemaProvider
		// + IIntegrationProviderValidator; the factory resolves them to seed schemas.
		// IValidationStatusStore is the in-memory variant (thread-safe per ADR 0067 §7.2).
		builder.Services.AddHeadscaleIntegration();
		builder.Services.AddRecaptchaV3Integration();
		builder.Services.TryAddSingleton<IValidationStatusStore, InMemoryValidationStatusStore>();
		builder.Services.TryAddSingleton<IIntegrationAtlasProvider>(sp =>
		{
			var schemaProviders = sp.GetServices<IIntegrationSchemaProvider>();
			var schemas = schemaProviders.SelectMany(p => p.GetSchemas());
			return new InMemoryIntegrationAtlasProvider(schemas);
		});
		builder.Services.AddSingleton<IIntegrationAtlasContext, AnchorIntegrationAtlasContext>();
		builder.Services.TryAddSingleton<ILiveAnnouncer, Sunfish.UIAdapters.Blazor.Maui.MauiLiveAnnouncer>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
