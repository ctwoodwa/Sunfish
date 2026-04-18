using System.Net;
using System.Security.Cryptography;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Federation.PatternC.Tests;

/// <summary>
/// End-to-end scenario tests for <b>Pattern C</b> from Sunfish spec §10.4 — the PM-company-
/// issues-contractor-portal-validates macaroon flow. Demonstrates that Phase B's macaroon
/// primitives and Phase D's federation peer model compose into a working short-lived bearer
/// credential system across two nodes without requiring IPFS or <c>SyncEnvelope</c> plumbing.
/// </summary>
/// <remarks>
/// <para>Scenario: Acme Rentals (PM) hires an inspector (Jim). Acme mints a macaroon with a
/// 48-hour TTL, a subject caveat binding the credential to Jim's identity, an action caveat
/// limiting Jim to <c>read</c>, and a resource-schema caveat limiting access to inspection
/// records. Jim presents the macaroon at a separate portal node; the portal validates the
/// HMAC chain against its copy of the shared root key and evaluates every caveat against
/// request context before granting access.</para>
/// <para>All five tests share a single <see cref="PatternCPortalFixture"/> via
/// <see cref="IClassFixture{TFixture}"/> — starting / stopping Kestrel per test would add
/// ~500ms each without changing coverage.</para>
/// </remarks>
public sealed class PatternCMacaroonPortalTests : IClassFixture<PatternCPortalFixture>
{
    private readonly PatternCPortalFixture _fx;

    public PatternCMacaroonPortalTests(PatternCPortalFixture fx)
    {
        _fx = fx;
    }

    /// <summary>
    /// Happy path — a valid 4-caveat macaroon with fully matching request context is accepted
    /// and the portal returns <c>200 OK</c>. Exercises time / subject / action / schema all
    /// passing in a single verification.
    /// </summary>
    [Fact]
    public async Task ValidMacaroon_WithMatchingContext_PortalAccepts()
    {
        var macaroon = await _fx.PmIssuer.MintAsync(
            PatternCPortalFixture.Location,
            "jim-inspection-2026-04-17",
            new[]
            {
                new Caveat($"time <= \"{DateTimeOffset.UtcNow.AddHours(48):O}\""),
                new Caveat("subject == \"individual:jim@acme-inspect\""),
                new Caveat("action in [\"read\"]"),
                new Caveat("resource.schema matches \"sunfish.pm.inspection/*\""),
            });
        var b64 = MacaroonCodec.EncodeBase64Url(macaroon);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/portal/inspections/unit-4b");
        req.Headers.Add("Authorization", $"Macaroon {b64}");
        req.Headers.Add("X-Subject-Uri", "individual:jim@acme-inspect");
        req.Headers.Add("X-Requested-Action", "read");
        req.Headers.Add("X-Resource-Schema", "sunfish.pm.inspection/unit-4b");

        using var response = await _fx.PortalClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Expired-time path — the macaroon itself is cryptographically valid (signature chain
    /// verifies) but its <c>time &lt;=</c> caveat evaluates to false at request time, so the
    /// portal denies with <c>401</c> and a "Caveat failed: time…" reason. Demonstrates that
    /// TTL enforcement is on the verifier, not the issuer.
    /// </summary>
    [Fact]
    public async Task ValidMacaroon_WithExpiredTime_PortalDenies()
    {
        var macaroon = await _fx.PmIssuer.MintAsync(
            PatternCPortalFixture.Location,
            "jim-expired-2026-04-17",
            new[]
            {
                new Caveat($"time <= \"{DateTimeOffset.UtcNow.AddSeconds(-1):O}\""),
                new Caveat("subject == \"individual:jim@acme-inspect\""),
                new Caveat("action in [\"read\"]"),
            });
        var b64 = MacaroonCodec.EncodeBase64Url(macaroon);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/portal/inspections/unit-4b");
        req.Headers.Add("Authorization", $"Macaroon {b64}");
        req.Headers.Add("X-Subject-Uri", "individual:jim@acme-inspect");
        req.Headers.Add("X-Requested-Action", "read");
        req.Headers.Add("X-Resource-Schema", "sunfish.pm.inspection/unit-4b");

        using var response = await _fx.PortalClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Caveat failed: time", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Wrong-subject path — the macaroon is valid, but the request context asserts a subject
    /// different from the one bound into the macaroon. The subject caveat fails; portal
    /// denies. Demonstrates that the contractor cannot "lend" the credential to someone else
    /// because the relying party re-evaluates the subject at the request.
    /// </summary>
    [Fact]
    public async Task ValidMacaroon_WithWrongSubject_PortalDenies()
    {
        var macaroon = await _fx.PmIssuer.MintAsync(
            PatternCPortalFixture.Location,
            "jim-subject-mismatch",
            new[]
            {
                new Caveat($"time <= \"{DateTimeOffset.UtcNow.AddHours(1):O}\""),
                new Caveat("subject == \"individual:jim@acme-inspect\""),
                new Caveat("action in [\"read\"]"),
            });
        var b64 = MacaroonCodec.EncodeBase64Url(macaroon);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/portal/inspections/unit-4b");
        req.Headers.Add("Authorization", $"Macaroon {b64}");
        // Not Jim — somebody else trying to use Jim's macaroon.
        req.Headers.Add("X-Subject-Uri", "individual:bob@acme-inspect");
        req.Headers.Add("X-Requested-Action", "read");
        req.Headers.Add("X-Resource-Schema", "sunfish.pm.inspection/unit-4b");

        using var response = await _fx.PortalClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Caveat failed: subject", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tamper path — flips one byte of the signature. The HMAC chain reconstruction at the
    /// portal produces a different signature; fixed-time compare fails; portal denies with
    /// "Signature mismatch". Demonstrates that a malicious intermediary cannot silently
    /// mutate the credential.
    /// </summary>
    [Fact]
    public async Task TamperedMacaroon_PortalDenies_SignatureMismatch()
    {
        var macaroon = await _fx.PmIssuer.MintAsync(
            PatternCPortalFixture.Location,
            "jim-tampered",
            new[]
            {
                new Caveat($"time <= \"{DateTimeOffset.UtcNow.AddHours(1):O}\""),
                new Caveat("subject == \"individual:jim@acme-inspect\""),
                new Caveat("action in [\"read\"]"),
            });

        // Flip the first byte of the signature.
        var tamperedSig = (byte[])macaroon.Signature.Clone();
        tamperedSig[0] ^= 0x01;
        var tampered = new Macaroon(macaroon.Location, macaroon.Identifier, macaroon.Caveats, tamperedSig);
        var b64 = MacaroonCodec.EncodeBase64Url(tampered);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/portal/inspections/unit-4b");
        req.Headers.Add("Authorization", $"Macaroon {b64}");
        req.Headers.Add("X-Subject-Uri", "individual:jim@acme-inspect");
        req.Headers.Add("X-Requested-Action", "read");
        req.Headers.Add("X-Resource-Schema", "sunfish.pm.inspection/unit-4b");

        using var response = await _fx.PortalClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Signature mismatch", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Unknown-location path — a macaroon minted by some OTHER issuer for a location the
    /// portal has no root key for. The portal's <see cref="IRootKeyStore"/> returns null, so
    /// the verifier bails out at the "No root key" stage before any caveat evaluation.
    /// Demonstrates that macaroons are scoped to locations, and an attacker cannot forge
    /// authority by naming a location the relying party has never onboarded.
    /// </summary>
    [Fact]
    public async Task Macaroon_FromUnknownLocation_PortalDenies_RootKeyMissing()
    {
        var altKeyStore = new InMemoryRootKeyStore();
        var altRootKey = new byte[32];
        RandomNumberGenerator.Fill(altRootKey);
        const string altLocation = "https://other-company.example/";
        altKeyStore.Set(altLocation, altRootKey);
        var altIssuer = new DefaultMacaroonIssuer(altKeyStore);

        var macaroon = await altIssuer.MintAsync(
            altLocation,
            "unknown-location-cred",
            new[]
            {
                new Caveat($"time <= \"{DateTimeOffset.UtcNow.AddHours(1):O}\""),
            });
        var b64 = MacaroonCodec.EncodeBase64Url(macaroon);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/portal/inspections/unit-4b");
        req.Headers.Add("Authorization", $"Macaroon {b64}");
        req.Headers.Add("X-Subject-Uri", "individual:jim@acme-inspect");
        req.Headers.Add("X-Requested-Action", "read");
        req.Headers.Add("X-Resource-Schema", "sunfish.pm.inspection/unit-4b");

        using var response = await _fx.PortalClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No root key", body, StringComparison.Ordinal);
    }
}
