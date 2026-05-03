using Sunfish.Anchor.Services;

namespace Sunfish.Anchor;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private CancellationTokenSource? _lifetimeCts;

	public App(IServiceProvider services)
	{
		ArgumentNullException.ThrowIfNull(services);
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "Sunfish.Anchor" };

		// Wave 6.3.F follow-up: MAUI's MauiApp does NOT implement IHost, so
		// AddHostedService<AnchorBootstrapHostedService>() in MauiProgram.cs
		// registers the service in DI but nothing drives StartAsync on it.
		// We manually pump the IHostedService lifecycle off the Window.Created
		// and Window.Destroying events, preserving the same
		// AddHostedService<T>() composition pattern used by apps/local-node-host.
		// See Services/MauiHostedServiceLifetime for the verified ground truth
		// on MAUI 10 behaviour.
		window.Created += OnWindowCreated;
		window.Destroying += OnWindowDestroying;

		return window;
	}

	private async void OnWindowCreated(object? sender, EventArgs e)
	{
		_lifetimeCts = new CancellationTokenSource();
		try
		{
			await MauiHostedServiceLifetime.StartAllAsync(_services, _lifetimeCts.Token)
				.ConfigureAwait(false);
		}
		catch
		{
			// Already logged inside the helper. Rethrowing from an async void
			// event handler would crash the process; let the bootstrap failure
			// surface downstream as a missing active team (which the UI guards
			// via QrOnboardingService's null-active-team guard).
		}
	}

	private async void OnWindowDestroying(object? sender, EventArgs e)
	{
		var cts = _lifetimeCts;
		_lifetimeCts = null;
		if (cts is null)
		{
			return;
		}

		try
		{
			// Generic-host default shutdown timeout is 30s; mirror that here
			// so we don't hang process exit if a hosted service misbehaves.
			using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await MauiHostedServiceLifetime.StopAllAsync(_services, stopCts.Token)
				.ConfigureAwait(false);
		}
		finally
		{
			cts.Dispose();
		}
	}
}
