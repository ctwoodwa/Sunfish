using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.Extensions;
using Sunfish.Foundation.LocalFirst;
using Sunfish.Kernel.Runtime.DependencyInjection;
using Sunfish.Kernel.Security.DependencyInjection;
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

		// Paper §11.2 Layer 1 — encrypted local store + keystore.
		builder.Services.AddSunfishEncryptedStore();

		// Paper §11.3 — Ed25519 signer, attestation issuer/verifier, role-key manager.
		builder.Services.AddSunfishKernelSecurity();

		// Paper §5.1 — node host + plugin registry.
		builder.Services.AddSunfishKernelRuntime();

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
