# W#23 Phase 5 — iOS Field-Capture Pairing Flow — Stage 06 hand-off

**Supersedes:** The Phase 5 section in the main hand-off
(`property-ios-field-app-stage06-handoff.md`). That file described Razor pages
and ViewModel classes; the actual P4+P4.5 build established different patterns
(Blazor `.razor` components, minimal-API static classes). **Use this addendum.**

**Workstream:** #23 · Phase 5 of the iOS Field-Capture App substrate v1
**Spec sources:** ADR 0028 + A1–A3, ADR 0048 + A1–A2, main hand-off §Phase 5
**Pipeline variant:** `sunfish-feature-change`
**Estimated effort:** ~4h sunfish-PM
**PR title:** `feat(anchor-mobile-ios,bridge,anchor,kernel-audit): W#23 P5 — pairing flow + 4 AuditEventType`

---

## Prerequisites

| Prerequisite | Status |
|---|---|
| W#23 P0 — `IPairingService` + `HmacPairingService` + `PairingToken` | ✓ shipped PR #478 |
| W#23 P1–P4.5 — SwiftUI scaffold + GRDB + EventEnvelope + SyncEngine | ✓ shipped PRs #498/#511/#516/#517/#533 |
| `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs` (minimal API) | ✓ shipped PR #533 |
| `AuditEventType.FieldEventAccepted` etc. section pattern | ✓ established PR #533 |

---

## Pairing protocol (v1 substrate)

The v1 design solves the Anchor↔Bridge shared-state problem via a
**register-then-claim** two-step:

1. **Anchor UI**: user clicks "Pair a Field Device"
2. **Anchor**: calls `IPairingService.IssuePairingTokenAsync(tenant, deviceId: "unbound-v1")` →
   `PairingToken`. The sentinel `"unbound-v1"` is intentional: the specific iOS
   device that will claim this token is unknown at issuance time. Device binding
   happens at Bridge claim time (step 8). Bridge does NOT re-verify the HMAC in v1
   (HMAC is stored as metadata only; cryptographic device-binding via HMAC is a
   production-hardening item requiring an ADR 0028 amendment).
3. **Anchor**: derives display code = `PairingToken.PairingTokenId[..8].ToUpperInvariant()`
   (e.g., `"ABCD1234"` — base32 alphabet only, chars `A-Z2-7`; UI must
   NOT include a "0" or "1" clarification since those chars don't appear)
4. **Anchor**: POSTs the full `PairingToken` to `POST /api/v1/field/pair-register` on Bridge
   (internal registration call)
5. **Anchor**: emits `FieldDevicePairingTokenIssued` audit event; shows display
   code + expiry time
6. **iPad**: user types display code in `PairingFlow` UI
7. **iOS `PairingService`**: `POST /api/v1/field/pair` with `{code, deviceId}`
8. **Bridge**: looks up code in its in-memory store; validates expiry; emits
   `FieldDevicePaired` (or `FieldDevicePairingTokenExpired` if expired); binds
   `deviceId` to the entry
9. **Bridge**: responds `{tenantId, bridgeBaseUrl, confirmedAt, bearerToken}`
10. **iOS**: stores bearer in Keychain; wires into `SyncEngine.pairingTokenBearer`

**V1 limitations (document in code comments; do NOT treat as blocking):**
- `pair-register` is unauthenticated — add `// TODO(W#23 P6): mTLS or shared-secret auth`
  comment + `LogWarning` per call so prod hardening has a metric
- Replay within TTL window is possible (no consumed-check once Bridge restarts;
  same posture as `_eventIdempotencyCache`)
- Bridge does NOT re-verify the HMAC (stored as metadata only); cryptographic
  device-binding via HMAC requires ADR 0028 amendment for production
- `IssuePairingTokenAsync` called with sentinel `"unbound-v1"` deviceId;
  device binding deferred to claim time; document in code comment

---

## Files

### 1. `packages/kernel-audit/AuditEventType.cs` — add 4 constants

**After** the OOD watch section (after `OodWatchExpired`), insert:

```csharp
// ===== ADR 0028-A2.8 + W#23 P5 — iOS field-device pairing flow =====

/// <summary>Anchor operator issued a pairing code via <c>IPairingService.IssuePairingTokenAsync</c>;
/// per W#23 P5 and ADR 0028-A2.8.</summary>
public static readonly AuditEventType FieldDevicePairingTokenIssued = new("FieldDevicePairingTokenIssued");

/// <summary>An iOS field-capture device successfully claimed a pairing code via
/// <c>POST /api/v1/field/pair</c>; per W#23 P5.</summary>
public static readonly AuditEventType FieldDevicePaired = new("FieldDevicePaired");

/// <summary>A pairing code expired before being claimed; detected lazily at Bridge claim
/// time; per W#23 P5.</summary>
public static readonly AuditEventType FieldDevicePairingTokenExpired = new("FieldDevicePairingTokenExpired");

/// <summary>A previously-paired field device was revoked by the Anchor operator; emitted
/// in the follow-up revocation phase (W#23 P6+).</summary>
public static readonly AuditEventType FieldDeviceRevoked = new("FieldDeviceRevoked");
```

Keep the `ToString()` override as the final member.

---

### 2. `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs` — add pair-register + pair

**Add to `MapFieldEndpoints()`** (after the existing blob route):

```csharp
group.MapPost("/pair-register", HandleFieldPairRegisterAsync);
group.MapPost("/pair", HandleFieldPairClaimAsync);
```

**Add in-memory store** (next to `_eventIdempotencyCache`):

```csharp
/// <summary>
/// In-process registered pairing code store. Keyed by 8-char display code
/// (upper-case). Populated by Anchor via <c>POST /api/v1/field/pair-register</c>;
/// claimed by iOS via <c>POST /api/v1/field/pair</c>. Restart-volatile (v1).
/// </summary>
private sealed record RegisteredPairingEntry(
    string TenantId,
    string PairingTokenId,
    DateTimeOffset ExpiresAt,
    string? BoundDeviceId);

private static readonly ConcurrentDictionary<string, RegisteredPairingEntry> _pairingCodeStore = new();
```

**Add wire-format records** (inside the class, near `FieldEventEnvelope`):

```csharp
/// <summary>Body of POST /api/v1/field/pair-register (Anchor → Bridge internal).</summary>
internal sealed record PairRegisterRequest(
    string TenantId,
    string PairingTokenId,
    string DisplayCode,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>Body of POST /api/v1/field/pair (iOS → Bridge).</summary>
internal sealed record PairClaimRequest(
    string Code,
    string DeviceId);
```

**`HandleFieldPairRegisterAsync` handler:**

```csharp
internal static async Task<IResult> HandleFieldPairRegisterAsync(
    HttpRequest request,
    IAuditTrail auditTrail,
    IOperationSigner signer,
    CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(auditTrail);
    ArgumentNullException.ThrowIfNull(signer);

    PairRegisterRequest? body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<PairRegisterRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            ct).ConfigureAwait(false);
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { error = "schema-validation-failed", detail = ex.Message });
    }

    if (body is null || string.IsNullOrWhiteSpace(body.DisplayCode) || body.DisplayCode.Length != 8)
        return Results.BadRequest(new { error = "invalid-display-code" });

    if (body.ExpiresAt <= DateTimeOffset.UtcNow)
        return Results.BadRequest(new { error = "already-expired" });

    var entry = new RegisteredPairingEntry(
        body.TenantId,
        body.PairingTokenId,
        body.ExpiresAt,
        BoundDeviceId: null);
    _pairingCodeStore[body.DisplayCode] = entry;

    // TODO(W#23 P6): mTLS or shared-secret auth on this endpoint before production.
    // Log a warning so prod hardening has a metric to chase.
    var logger = request.HttpContext.RequestServices
        .GetService<Microsoft.Extensions.Logging.ILogger<FieldEndpoints>>();
    logger?.LogWarning(
        "pair-register called without authentication (v1 substrate). " +
        "Code={DisplayCode} TenantId={TenantId} ExpiresAt={ExpiresAt:O}",
        body.DisplayCode, body.TenantId, body.ExpiresAt);

    return Results.Ok(new { registered = true, displayCode = body.DisplayCode });
}
```

**`HandleFieldPairClaimAsync` handler:**

```csharp
internal static async Task<IResult> HandleFieldPairClaimAsync(
    HttpRequest request,
    IAuditTrail auditTrail,
    IOperationSigner signer,
    CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(auditTrail);
    ArgumentNullException.ThrowIfNull(signer);

    PairClaimRequest? body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<PairClaimRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            ct).ConfigureAwait(false);
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { error = "schema-validation-failed", detail = ex.Message });
    }

    if (body is null || string.IsNullOrWhiteSpace(body.Code) || string.IsNullOrWhiteSpace(body.DeviceId))
        return Results.BadRequest(new { error = "missing-code-or-device-id" });

    var displayCode = body.Code.ToUpperInvariant();
    if (!_pairingCodeStore.TryGetValue(displayCode, out var entry))
    {
        return Results.NotFound(new { error = "pairing-code-not-found" });
    }

    var now = DateTimeOffset.UtcNow;
    if (entry.ExpiresAt <= now)
    {
        _pairingCodeStore.TryRemove(displayCode, out _);
        await EmitAuditAsync(auditTrail, signer,
            AuditEventType.FieldDevicePairingTokenExpired,
            new TenantId(entry.TenantId),
            new AuditPayload(new Dictionary<string, object?>
            {
                ["display_code"] = displayCode,
                ["pairing_token_id"] = entry.PairingTokenId,
                ["expired_at"] = entry.ExpiresAt.ToString("O"),
            }),
            ct).ConfigureAwait(false);
        return Results.StatusCode(StatusCodes.Status410Gone);
    }

    // Single-shot claim: CAS-remove the entry before emitting the bearer.
    // If two requests race, only the first succeeds; the second gets 404
    // (entry already removed). This prevents double-bearer issuance on
    // concurrent claims.
    // TODO(W#23 P6): emit FieldDevicePairingAlreadyClaimed on the 404 path.
    if (!_pairingCodeStore.TryRemove(displayCode, out _))
    {
        return Results.NotFound(new { error = "pairing-code-not-found" });
    }

    await EmitAuditAsync(auditTrail, signer,
        AuditEventType.FieldDevicePaired,
        new TenantId(entry.TenantId),
        new AuditPayload(new Dictionary<string, object?>
        {
            ["device_id"] = body.DeviceId,
            ["display_code"] = displayCode,
            ["pairing_token_id"] = entry.PairingTokenId,
        }),
        ct).ConfigureAwait(false);

    return Results.Ok(new
    {
        tenantId = entry.TenantId,
        bridgeBaseUrl = $"{request.Scheme}://{request.Host}",
        confirmedAt = now.ToString("O"),
        bearerToken = entry.PairingTokenId,
    });
}
```

---

### 3. `accelerators/anchor/Components/Pages/FieldPairingPage.razor` — new Blazor page

**Create** this file (new; no existing file):

```razor
@page "/field/pairing"
@using Microsoft.Extensions.Logging
@using Sunfish.Anchor.Services
@using Sunfish.Anchor.Services.Pairing
@using Sunfish.Foundation.Assets.Common
@using Sunfish.Kernel.Audit
@using System.Net.Http.Json
@inject IPairingService PairingService
@inject IAuditTrail AuditTrail
@inject IOperationSigner OperationSigner
@inject AnchorSessionService Session
@inject IHttpClientFactory HttpFactory
@inject ILogger<FieldPairingPage> Logger
@inject TimeProvider Time

<PageTitle>Pair a Field Device — Sunfish Anchor</PageTitle>

<div class="anchor-pairing">
    <h1>Pair a Field Device</h1>
    <p>Generate a code and share it with the iOS field-capture app.</p>

    @if (_code is null)
    {
        <button class="anchor-btn anchor-btn-primary"
                @onclick="IssueCodeAsync"
                disabled="@_issuing"
                aria-busy="@_issuing">
            @(_issuing ? "Generating…" : "Generate Pairing Code")
        </button>
    }
    else
    {
        <div class="anchor-pairing-code-card" role="region"
             aria-label="Pairing code — share this with the field device">
            <p class="anchor-pairing-instructions">
                Type this code into the Sunfish Field app on your iOS device.
            </p>
            <div class="anchor-pairing-code" aria-label="Pairing code: @_code">
                @_code
            </div>
            <p class="anchor-pairing-expiry">
                Valid until @_expiresAt?.ToString("HH:mm:ss zzz") (10&nbsp;minutes).
            </p>
        </div>

        @if (_error is not null)
        {
            <p class="anchor-error" role="alert">@_error</p>
        }

        <button class="anchor-btn anchor-btn-secondary"
                @onclick="ClearCode"
                aria-label="Generate a new pairing code">
            Generate New Code
        </button>
    }
</div>

@code {
    private string? _code;
    private DateTimeOffset? _expiresAt;
    private string? _error;
    private bool _issuing;

    private async Task IssueCodeAsync()
    {
        _issuing = true;
        _error = null;
        StateHasChanged();
        try
        {
            // TeamId is null before onboarding completes. Use System sentinel
            // so the audit record is traceable even for pre-onboarded pairing
            // attempts. Halt-condition H6: if TeamId is null here, the operator
            // hasn't onboarded yet — show an error instead of issuing a code.
            if (string.IsNullOrWhiteSpace(Session.TeamId))
            {
                _error = "Anchor is not yet onboarded to a team. Complete onboarding first.";
                return;
            }
            var tenantId = new TenantId(Session.TeamId);
            // Issue with sentinel deviceId "unbound-v1" — device binding happens at
            // Bridge claim time (claim step binds the iOS deviceId to the token entry).
            // Bridge does NOT re-verify HMAC in v1; HMAC is stored as metadata only.
            var token = await PairingService.IssuePairingTokenAsync(tenantId, deviceId: "unbound-v1", default);
            var displayCode = token.PairingTokenId.Length >= 8
                ? token.PairingTokenId[..8].ToUpperInvariant()
                : token.PairingTokenId.ToUpperInvariant();

            // Register with Bridge so the iOS app can claim by short code.
            var bridgeBaseUrl = "http://localhost:5050"; // TODO: read from Anchor config
            var http = HttpFactory.CreateClient("SunfishBridge");
            var registerResp = await http.PostAsJsonAsync(
                $"{bridgeBaseUrl}/api/v1/field/pair-register",
                new
                {
                    tenantId = tenantId.Value,
                    pairingTokenId = token.PairingTokenId,
                    displayCode,
                    issuedAt = token.IssuedAt.ToString("O"),
                    expiresAt = token.ExpiresAt.ToString("O"),
                });

            if (!registerResp.IsSuccessStatusCode)
            {
                _error = $"Failed to register with Bridge (HTTP {(int)registerResp.StatusCode}).";
                return;
            }

            // Emit audit.
            var occurredAt = Time.GetUtcNow();
            var nonce = Guid.NewGuid();
            var payload = new AuditPayload(new System.Collections.Generic.Dictionary<string, object?>
            {
                ["display_code"] = displayCode,
                ["pairing_token_id"] = token.PairingTokenId,
            });
            var signed = await OperationSigner.SignAsync(payload, occurredAt, nonce, default);
            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: tenantId,
                EventType: AuditEventType.FieldDevicePairingTokenIssued,
                OccurredAt: occurredAt,
                Payload: signed,
                AttestingSignatures: System.Collections.Immutable.ImmutableArray<AttestingSignature>.Empty);
            await AuditTrail.AppendAsync(record, default);

            _code = displayCode;
            _expiresAt = token.ExpiresAt;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to issue pairing code");
            _error = "Failed to issue pairing code. Check logs.";
        }
        finally
        {
            _issuing = false;
        }
    }

    private void ClearCode()
    {
        _code = null;
        _expiresAt = null;
        _error = null;
    }
}
```

**Note on `bridgeBaseUrl`:** For substrate v1, hardcode `http://localhost:5050` with a
`// TODO: read from Anchor config` comment. Production will inject via
`Microsoft.Extensions.Options` (a follow-up `FieldPairingOptions` record). Do
NOT block the PR on this — the v1 hardcode is intentional substrate scope.

---

### 4. `accelerators/anchor/MauiProgram.cs` — register `IPairingService` + HTTP client

**Add** before the existing `AddHostedService` registrations (suggested placement:
after `AddSunfishCrewComms`):

```csharp
// W#23 P5 — pairing service for iOS Field-Capture device pairing.
// TimeProvider.System is used directly; MAUI does not register TimeProvider
// in DI by default. If a test-overridable provider is needed later, add
// builder.Services.AddSingleton(TimeProvider.System) and inject via DI.
builder.Services.AddSingleton<IPairingService, HmacPairingService>(sp =>
    new HmacPairingService(
        sp.GetRequiredService<Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider>(),
        TimeProvider.System));

// W#23 P5 — IHttpClientFactory + named "SunfishBridge" client for pair-register calls.
// AddHttpClient() must be called; MAUI does not add IHttpClientFactory by default.
builder.Services.AddHttpClient("SunfishBridge", static c =>
    c.DefaultRequestHeaders.Add("User-Agent", "Sunfish-Anchor/1.0"));
```

**Halt-condition**: if `ITenantKeyProvider` is not registered in Anchor's DI
by this point, the `GetRequiredService<ITenantKeyProvider>()` will throw at
container build time. Check with `MauiProgram.cs` and search for existing
`ITenantKeyProvider` registrations. If missing, fall back to the `HmacPairingService`
two-arg constructor (using `TimeProvider.System` default) and register
`ITenantKeyProvider` from `Sunfish.Foundation.Recovery` before this line.

---

### 5. `accelerators/anchor-mobile-ios/SunfishField/Onboarding/PairingResult.swift` — create

```swift
import Foundation

/// Per W#23 P5 — result returned by Bridge on successful pairing-code claim.
public struct PairingResult: Codable, Sendable {
    /// Tenant the device is now paired to.
    public let tenantId: String
    /// Bridge base URL to use for subsequent sync (stored in Keychain).
    public let bridgeBaseUrl: String
    /// UTC instant Bridge confirmed the pairing.
    public let confirmedAt: Date
    /// Bearer token to include in `Authorization: Bearer <token>` on sync
    /// requests. This is the PairingTokenId from the registered PairingToken.
    public let bearerToken: String
}
```

---

### 6. `accelerators/anchor-mobile-ios/SunfishField/Onboarding/PairingService.swift` — create

```swift
import Foundation

/// Per W#23 P5 — iOS-side service that claims a pairing code from Bridge
/// and binds the install identity to a specific tenant.
public final class PairingService: Sendable {
    private let bridgeBaseUrl: URL

    public init(bridgeBaseUrl: URL) {
        self.bridgeBaseUrl = bridgeBaseUrl
    }

    /// Submit the operator-displayed pairing code to Bridge and receive a
    /// `PairingResult` binding this device to the operator's tenant.
    ///
    /// - Parameters:
    ///   - code: 8-character alphanumeric code displayed in Anchor UI.
    ///   - deviceId: This install's `DeviceId.value` (hex-derived from Ed25519 pubkey).
    /// - Returns: `PairingResult` on success.
    /// - Throws: `PairingError` on failure.
    public func pairAsync(code: String, deviceId: String) async throws -> PairingResult {
        let url = bridgeBaseUrl.appendingPathComponent("api/v1/field/pair")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let body = PairClaimBody(code: code.uppercased(), deviceId: deviceId)
        request.httpBody = try JSONEncoder.sunfishFieldEncoder.encode(body)

        let (data, response) = try await URLSession.shared.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw PairingError.networkFailure(nil)
        }
        switch httpResponse.statusCode {
        case 200:
            let decoder = JSONDecoder()
            decoder.dateDecodingStrategy = .iso8601
            return try decoder.decode(PairingResult.self, from: data)
        case 404:
            throw PairingError.codeNotFound
        case 410:
            throw PairingError.codeExpired
        default:
            throw PairingError.unexpectedStatus(httpResponse.statusCode)
        }
    }
}

/// Wire-format body for POST /api/v1/field/pair.
private struct PairClaimBody: Encodable {
    let code: String
    let deviceId: String
}

public enum PairingError: Error, Sendable {
    case codeNotFound
    case codeExpired
    case networkFailure(Error?)
    case unexpectedStatus(Int)
}

private extension JSONEncoder {
    static let sunfishFieldEncoder: JSONEncoder = {
        let e = JSONEncoder()
        e.keyEncodingStrategy = .convertToSnakeCase
        return e
    }()
}
```

---

### 7. `accelerators/anchor-mobile-ios/SunfishField/Onboarding/PairingFlow.swift` — create

SwiftUI three-screen pairing flow:

```swift
import SwiftUI

/// Per W#23 P5 — SwiftUI pairing flow presented as a full-screen sheet on
/// first launch (when no pairing result is found in Keychain).
///
/// Screen 1 — Code entry: operator reads the 8-char code from Anchor.
/// Screen 2 — In-progress: shows spinner while Bridge validates.
/// Screen 3 — Success/failure: "Paired!" or error with retry option.
public struct PairingFlow: View {
    @State private var code: String = ""
    @State private var phase: PairingPhase = .entry
    @State private var errorMessage: String? = nil

    private let identity: InstallIdentity
    private let bridgeBaseUrl: URL
    private let onPaired: (PairingResult) -> Void

    public init(
        identity: InstallIdentity,
        bridgeBaseUrl: URL,
        onPaired: @escaping (PairingResult) -> Void
    ) {
        self.identity = identity
        self.bridgeBaseUrl = bridgeBaseUrl
        self.onPaired = onPaired
    }

    public var body: some View {
        NavigationStack {
            switch phase {
            case .entry:
                entryView
            case .pairing:
                pairingProgressView
            case .success(let result):
                successView(result: result)
            case .failure(let msg):
                failureView(message: msg)
            }
        }
    }

    // MARK: — Entry screen

    private var entryView: some View {
        VStack(spacing: 24) {
            Text("Connect to Anchor")
                .font(.largeTitle.bold())
            Text("Ask the Anchor desktop operator for the 8-character pairing code.")
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
            TextField("Pairing Code (e.g. ABCD1234)", text: $code)
                .textInputAutocapitalization(.characters)
                .autocorrectionDisabled()
                .keyboardType(.asciiCapable)
                .textFieldStyle(.roundedBorder)
                .padding(.horizontal)
            if let msg = errorMessage {
                Text(msg)
                    .foregroundStyle(.red)
                    .font(.callout)
            }
            Button("Pair Device") {
                Task { await submitCode() }
            }
            .buttonStyle(.borderedProminent)
            .disabled(code.trimmingCharacters(in: .whitespaces).count < 1)
        }
        .padding()
        .navigationTitle("Pair Device")
    }

    // MARK: — In-progress screen

    private var pairingProgressView: some View {
        VStack(spacing: 16) {
            ProgressView()
                .progressViewStyle(.circular)
                .scaleEffect(1.5)
            Text("Pairing…")
                .foregroundStyle(.secondary)
        }
        .navigationTitle("Pairing")
    }

    // MARK: — Success screen

    private func successView(result: PairingResult) -> some View {
        VStack(spacing: 20) {
            Image(systemName: "checkmark.seal.fill")
                .font(.system(size: 60))
                .foregroundStyle(.green)
            Text("Device Paired!")
                .font(.title.bold())
            Text("This device is now connected to tenant \(result.tenantId).")
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
        }
        .padding()
        .navigationTitle("Paired")
    }

    // MARK: — Failure screen

    private func failureView(message: String) -> some View {
        VStack(spacing: 20) {
            Image(systemName: "xmark.seal.fill")
                .font(.system(size: 60))
                .foregroundStyle(.red)
            Text("Pairing Failed")
                .font(.title.bold())
            Text(message)
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
            Button("Try Again") { phase = .entry; errorMessage = nil }
                .buttonStyle(.borderedProminent)
        }
        .padding()
        .navigationTitle("Pairing Failed")
    }

    // MARK: — Logic

    private func submitCode() async {
        let trimmed = code.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return }
        phase = .pairing
        let service = PairingService(bridgeBaseUrl: bridgeBaseUrl)
        do {
            let result = try await service.pairAsync(
                code: trimmed,
                deviceId: identity.deviceId.value)
            phase = .success(result)
            onPaired(result)
        } catch PairingError.codeNotFound {
            phase = .failure("Code not found — check that Anchor shows this code as active.")
        } catch PairingError.codeExpired {
            phase = .failure("Code expired — ask the Anchor operator to generate a new one.")
        } catch {
            phase = .failure("Network error: \(error.localizedDescription)")
        }
    }
}

private enum PairingPhase {
    case entry
    case pairing
    case success(PairingResult)
    case failure(String)
}
```

---

### 8. `accelerators/anchor-mobile-ios/SunfishField/App/SunfishFieldApp.swift` — show PairingFlow on first launch

**Modify** the `@main` App struct in `SunfishField/SunfishFieldApp.swift` to check for
a stored `PairingResult` in Keychain and present `PairingFlow` as a full-screen
sheet when absent.

For substrate v1, use a simple `@AppStorage` bool `isPaired` as a proxy for
Keychain state (full Keychain persistence is a follow-up):

```swift
// In SunfishFieldApp body or ContentView:
if !isPaired {
    PairingFlow(
        identity: InstallIdentity.current,   // loaded/generated once at app start
        bridgeBaseUrl: URL(string: "http://localhost:5050")!,  // TODO: from config
        onPaired: { result in
            // Store result; flip isPaired.
            isPaired = true
        }
    )
} else {
    ContentView()
}
```

`InstallIdentity.current` is a static accessor that loads from Keychain (via
`InstallIdentity+Keychain.load`) or generates + persists a new one if absent.
**If this static accessor doesn't exist yet** (P0 only exposes `generate()` and
`InstallIdentity+Keychain` helpers), add it:

```swift
// In InstallIdentity.swift or InstallIdentity+Keychain.swift:
public static var current: InstallIdentity {
    if let loaded = (try? InstallIdentity.load(account: "sunfish-field-install")) {
        return loaded
    }
    let fresh = InstallIdentity.generate()
    try? InstallIdentity.persist(fresh, account: "sunfish-field-install")
    return fresh
}
```

---

### 9. Test files

#### `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Field/FieldPairingEndpointTests.cs` (new)

Minimum tests (use the same handler-invocation pattern as the P4.5 event endpoint tests if any exist; otherwise use `Microsoft.AspNetCore.TestHost`):

1. **`RegisterThenClaim_ReturnsOkWithBearerToken`** — register a code → claim with correct code + deviceId → 200 OK + bearer in response
2. **`Claim_UnknownCode_Returns404`** — POST `/pair` with unregistered code → 404
3. **`Claim_ExpiredCode_Returns410AndEmitsExpiredAudit`** — register code with `ExpiresAt = UtcNow - 1s` → claim → 410 Gone + `FieldDevicePairingTokenExpired` in the in-memory audit trail
4. **`Register_MalformedBody_Returns400`** — POST `/pair-register` with missing fields → 400
5. **`Claim_EmitsFieldDevicePairedAudit`** — successful claim → `FieldDevicePaired` in audit trail with `device_id` + `pairing_token_id` keys

#### `accelerators/anchor-mobile-ios/Tests/SunfishFieldOnboardingTests/PairingServiceTests.swift` (new)

1. **`pairAsync_successResponse_returnsPairingResult`** — stub URLSession returning valid JSON → `PairingResult` decoded correctly
2. **`pairAsync_404_throwsCodeNotFound`** — stub returning 404 → `PairingError.codeNotFound`
3. **`pairAsync_410_throwsCodeExpired`** — stub returning 410 → `PairingError.codeExpired`
4. **`installIdentityCurrent_isIdempotent`** — call `InstallIdentity.current` twice (with Keychain reset between app-launches in test) → same `deviceId` returned; verifies `InstallIdentity.current` static accessor persists across calls (new surface added in P5)

**Package.swift addition** (new test target in existing `Package.swift`):

```swift
.testTarget(
    name: "SunfishFieldOnboardingTests",
    dependencies: ["SunfishField"],
    path: "Tests/SunfishFieldOnboardingTests"
),
```

---

## Halt-conditions

| # | Condition | Action |
|---|---|---|
| H1 | `ITenantKeyProvider` not registered in Anchor DI | Register `AddSunfishRecoveryCoordinator()` before the pairing service; halt if that pulls in types that don't build in MAUI context |
| H2 | `IHttpClientFactory` not available in Anchor (MAUI doesn't add it by default) | `builder.Services.AddHttpClient()` call may need to come before the named client registration; verify the Anchor `.csproj` references `Microsoft.Extensions.Http` |
| H3 | `InstallIdentity.load(account:)` or `InstallIdentity.persist(_:account:)` static helpers don't exist yet | Add them to `InstallIdentity+Keychain.swift` (they should have been in P0 substrate per the Phase 0 hand-off; if missing, that's a P0 oversight — add inline without a separate PR) |
| H4 | `SunfishFieldApp.swift` uses `#if SWIFT_PACKAGE` guard and `@AppStorage` isn't available in SPM-only build | Keep the `@AppStorage`-based pairing check inside `#if !SWIFT_PACKAGE` (Xcode build only); SPM build can stub with a `ContentView` placeholder |
| H5 | Bridge `pair-register` endpoint conflicts with the existing `/api/v1/field` group scope | Verify `MapGroup("/api/v1/field")` in `MapFieldEndpoints()` correctly prefixes both new routes |
| H6 | `AnchorSessionService.TeamId` is null (Anchor not onboarded) when user navigates to `/field/pairing` | Guard added in `IssueCodeAsync` — shows user-visible error before calling `IPairingService`; no halt-beacon needed |

---

## Acceptance criteria

- [ ] `packages/kernel-audit/AuditEventType.cs` — 4 new constants alphabetized within their section; `dotnet build packages/kernel-audit` clean
- [ ] Bridge: `POST /api/v1/field/pair-register` stores entry + returns `{registered: true}`; `POST /api/v1/field/pair` returns 200 on valid claim, 404 on unknown, 410 on expired
- [ ] Bridge: `FieldDevicePaired` emitted on successful claim with `device_id` + `pairing_token_id` in payload
- [ ] Bridge: `FieldDevicePairingTokenExpired` emitted when claim attempted on expired entry
- [ ] Bridge unit tests: 5 tests, all green
- [ ] Anchor: `FieldPairingPage.razor` navigable at `/field/pairing`; "Generate Pairing Code" button visible; code appears after click; `FieldDevicePairingTokenIssued` in audit trail after successful issue
- [ ] `HmacPairingService` registered in `MauiProgram.cs`; Anchor builds clean
- [ ] iOS: `PairingFlow` compiles in Xcode and SPM-side (`swift build`) clean
- [ ] iOS: `PairingService.pairAsync(code:deviceId:)` round-trips through the Bridge test-host (manual smoke test): Anchor page issues code → Bridge registers → iOS calls claim → 200 OK + PairingResult
- [ ] `accelerators/anchor-mobile-ios/Package.swift` updated with `SunfishFieldOnboardingTests` target
- [ ] iOS unit tests: 3 tests, all green
- [ ] **Pre-merge council** mandatory per ADR 0069 D1 (substrate PR; audit + security surface)

---

## What's not in scope (P5 deferrals)

- `FieldDeviceRevoked` audit event emission (no revocation endpoint in P5; constant declared, emission deferred to P6+ revocation flow)
- Keychain persistence of `PairingResult` (P5 uses `@AppStorage` bool; full persistence is a follow-up)
- `FieldPairingOptions` record for Bridge URL injection (TODO comment in code; `IOptions<T>` wiring is follow-up)
- QR code display in Anchor (show text code only; QR rendering requires a library install — separate PR)
- Bridge `pair-register` authentication (v1 is unauthenticated; mutual TLS / shared secret is production hardening)
- `pair-register` → consumed-check reconciliation on Bridge restart (restart-volatile v1 posture matches `_eventIdempotencyCache`)

---

## PR title + commit guidance

```
feat(anchor-mobile-ios,bridge,anchor,kernel-audit): W#23 P5 — pairing flow + 4 AuditEventType
```

Commit body should call out the 4-way change (iOS, Bridge, Anchor, kernel-audit)
and name all new AuditEventType constants. Dispatch **pre-merge security-engineering
council** before enabling auto-merge (new auth surface: `pair-register` +
`pair` endpoints + bearer token wiring).

---

## References

- `property-ios-field-app-stage06-handoff.md` §Phase 5 — original spec (superseded by this addendum for file paths + patterns)
- `accelerators/anchor/Services/Pairing/IPairingService.cs` — issued type; P0 PR #478
- `accelerators/anchor/Services/Pairing/HmacPairingService.cs` — HMAC impl; P0 PR #478
- `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs` — existing field route pattern; P4+P4.5 PR #533
- `packages/kernel-audit/AuditEventType.cs` — add after the OOD watch section (last section before `ToString()`)
- `accelerators/anchor-mobile-ios/Sources/Identity/` — P0 identity substrate
- `accelerators/anchor-mobile-ios/SunfishField/Sync/SyncEngine.swift` — `pairingTokenBearer` field (already present; P5 wires it)
