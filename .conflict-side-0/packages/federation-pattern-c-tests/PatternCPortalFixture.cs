using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Federation.PatternC.Tests.Endpoints;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Federation.PatternC.Tests;

/// <summary>
/// xUnit fixture for the Pattern C worked example (spec §10.4 — contractor portal with
/// macaroon-bound access). Simulates two federated Sunfish nodes:
/// <list type="bullet">
///   <item>A <b>PM company</b> side — only the issuer + its root-key store are held in-process
///   on the test; no HTTP host is needed since the PM mints macaroons and hands them to the
///   contractor out-of-band (email, URL).</item>
///   <item>A <b>portal</b> side — hosted as a real in-process Kestrel <see cref="WebApplication"/>
///   bound to <c>127.0.0.1:0</c>, with its own <see cref="InMemoryRootKeyStore"/> pre-loaded
///   with the same 32-byte root key for the <see cref="Location"/> (modeling the post-
///   onboarding state between two federation peers).</item>
/// </list>
/// <para>This fixture deliberately does not exercise <c>SyncEnvelope</c> — Pattern C's macaroon
/// flows out-of-band, not over federation messages. That is the whole point of the pattern:
/// short-lived bearer credentials travel via whatever transport fits the scenario, and the
/// portal validates them without any live federation dependency beyond the pre-shared root
/// key established at peer-onboarding.</para>
/// </summary>
public sealed class PatternCPortalFixture : IAsyncLifetime
{
    /// <summary>The location URI both the PM and portal agree on for this scenario.</summary>
    public const string Location = "https://acme-rentals.sunfish.example/";

    /// <summary>The 32-byte HMAC-SHA256 root key provisioned to both nodes.</summary>
    public byte[] SharedRootKey { get; } = new byte[32];

    /// <summary>The portal's in-process Kestrel <see cref="WebApplication"/> (PM side is headless).</summary>
    public WebApplication PortalApp { get; private set; } = default!;

    /// <summary>HTTP client bound to the portal's ephemeral base URL.</summary>
    public HttpClient PortalClient { get; private set; } = default!;

    /// <summary>PM-side in-memory root-key store (pre-loaded with <see cref="SharedRootKey"/>).</summary>
    public InMemoryRootKeyStore PmKeyStore { get; } = new();

    /// <summary>PM-side issuer used by the <c>ValidMacaroon_*</c> tests.</summary>
    public DefaultMacaroonIssuer PmIssuer { get; private set; } = default!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        RandomNumberGenerator.Fill(SharedRootKey);
        PmKeyStore.Set(Location, SharedRootKey);
        PmIssuer = new DefaultMacaroonIssuer(PmKeyStore);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // Portal's root-key store: pre-populated with the same key that the PM has. In
        // production this would be exchanged during federation peer-onboarding, e.g. via an
        // out-of-band key-agreement ceremony or a trusted admin channel. In test we stuff it
        // directly — the point is that by the time a macaroon arrives at the portal, the
        // portal already holds the root key for that location.
        var portalKeyStore = new InMemoryRootKeyStore();
        portalKeyStore.Set(Location, SharedRootKey);
        builder.Services.AddSingleton<IRootKeyStore>(portalKeyStore);
        builder.Services.AddSingleton<IMacaroonVerifier, DefaultMacaroonVerifier>();

        PortalApp = builder.Build();
        PortalApp.UseMiddleware<MacaroonAuthMiddleware>();
        PortalApp.MapInspectionEndpoint();

        await PortalApp.StartAsync();
        PortalClient = new HttpClient { BaseAddress = new Uri(PortalApp.Urls.First()) };
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        PortalClient?.Dispose();
        if (PortalApp is not null)
        {
            await PortalApp.DisposeAsync();
        }
    }
}
