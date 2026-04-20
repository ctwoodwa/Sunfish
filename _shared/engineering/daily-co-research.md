# Daily.co — Technical Research for Sunfish / Bridge

**Evaluated:** 2026-04-20  
**License:** SaaS (hosted infrastructure, no self-hosting option)  
**SDKs:** `@daily-co/daily-js` (vanilla JS), `@daily-co/daily-react` (React hooks), REST API (server-side)

---

## Summary

Daily.co is a hosted WebRTC infrastructure platform — they operate SFU media servers, TURN/STUN relay infrastructure, and CDN. You provision rooms via REST API (C# `HttpClient`) and connect participants via browser JS SDK. There is no .NET or WASM SDK; Blazor integration requires JS interop.

**Key finding:** The server-side (room provisioning, token minting) is pure REST — a clean C# wrapper. The client-side (media) is JS-only and runs in the browser's WebRTC stack. This clean split makes Bridge integration well-defined.

**Blocker for strict on-prem requirements:** No self-hosted option. All media routes through Daily's infrastructure.

---

## Core Capabilities

| Feature | Status | Notes |
|---|---|---|
| Audio/video calls | GA | Up to 300 active participants in standard mode |
| Interactive live streaming | GA | Up to 100,000 viewers, < 400ms latency, 25 active senders |
| Cloud recording | GA | `type: "cloud"`, `"cloud-audio-only"`, `"raw-tracks"` (individual per-participant MP4s) |
| Local recording | GA | Records to user's browser download |
| Transcription | GA | Deepgram-powered; `startTranscription({ language, model })` |
| Noise cancellation | GA | Browser-native on desktop, mobile Safari 17.4.1+ |
| Background blur / replacement | GA | WebGL-based, built into daily-js |
| Breakout rooms | Beta | Daily Prebuilt only; not available in custom call object mode |
| Screen sharing | GA | `type: 'screenVideo'` track |
| RTMP live streaming | GA | Multiple endpoints; configurable bitrate/fps/resolution |
| HLS output | GA | |
| Data messages / in-call chat | GA | `call.sendAppMessage()` — pub/sub to all or specific participants |
| Large calls (1,000–100,000) | GA | Requires `enable_mesh_sfu` room flag; domain-level enablement |

---

## SDK Options

### `@daily-co/daily-js` — Vanilla JS (foundation for all integrations)

Two factory methods — mutually exclusive per call instance:

```javascript
// Option A: Daily Prebuilt UI inside a managed iframe
const call = Daily.createFrame(containerElement, {
  iframeStyle: { width: '100%', height: '600px' }
});

// Option B: Headless call object — build 100% of your own UI
const call = Daily.createCallObject();
```

Supports `dailyConfig: { avoidEval: true }` for CSP-friendly environments (no `unsafe-eval`).

### `@daily-co/daily-react` — React hooks

```jsx
<DailyProvider url={roomUrl}>
  <VideoGrid />   {/* your custom component */}
</DailyProvider>
```

Key hooks: `useDaily()`, `useParticipantIds()`, `useParticipantProperty()`, `useParticipant()`, `useDailyEvent()`  
Key component: `<DailyVideo sessionId={...} type="video|screenVideo" />` — binds a `<video>` element to a participant's track.

### REST API (server-side, C# HttpClient)

Standard HTTPS + Bearer token. Used for:
- Room CRUD (`POST /v1/rooms`, `GET /v1/rooms/{name}`, `DELETE /v1/rooms/{name}`)
- Meeting token minting (`POST /v1/meeting-tokens`)
- Recording control, transcription, live stream control

This is the **Blazor server component** — no JS involved for room management.

---

## Blazor / WebAssembly Integration Pattern

No .NET or WASM SDK exists. The integration is JS interop throughout.

### Server-side (C#) — Room Management

```csharp
public class DailyRoomService : IDailyRoomService
{
    private readonly HttpClient _http;

    public async Task<DailyRoom> CreateRoomAsync(DateTimeOffset start, DateTimeOffset end)
    {
        var payload = new {
            properties = new {
                nbf = start.ToUnixTimeSeconds(),
                exp = end.ToUnixTimeSeconds(),
                start_audio_off = true
            }
        };
        var response = await _http.PostAsJsonAsync("/v1/rooms", payload);
        return await response.Content.ReadFromJsonAsync<DailyRoom>();
    }

    public async Task<string> MintTokenAsync(string roomName, string userId)
    {
        // Short-lived token minted per-attendee at join time
        var payload = new { properties = new { room_name = roomName, user_id = userId, exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds() } };
        var response = await _http.PostAsJsonAsync("/v1/meeting-tokens", payload);
        var result = await response.Content.ReadFromJsonAsync<DailyTokenResponse>();
        return result.Token;
    }
}
```

### Client-side (Blazor → JS Interop Bridge)

```javascript
// daily-bridge.js — custom JS interop layer
window.dailyBridge = {
  async join(roomUrl, token, dotNetRef) {
    const call = Daily.createCallObject();
    call.on('participant-joined', (e) => dotNetRef.invokeMethodAsync('OnParticipantJoined', e.participant));
    call.on('track-started', (e) => attachTrackToVideo(e));
    await call.join({ url: roomUrl, token });
  },
  async leave() { await _call.leave(); }
};
```

```csharp
// Blazor component
[JSInvokable]
public void OnParticipantJoined(DailyParticipant participant)
{
    _participants.Add(participant);
    StateHasChanged();
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
        await JS.InvokeVoidAsync("dailyBridge.join", RoomUrl, Token, DotNetObjectReference.Create(this));
}
```

Video rendering: render `<video id="tile-{sessionId}">` in Razor markup; the JS bridge calls `call.getParticipantVideoTrack()` and attaches tracks to those elements by ID.

---

## Integration Patterns

### Pattern A — Daily Prebuilt (lowest effort, fastest ship)

Complete Daily UI inside an iframe — no video tile code, no layout logic.

```javascript
const call = Daily.createFrame(containerElement, { iframeStyle: { width: '100%', height: '600px' } });
call.join({ url: roomUrl, token: meetingToken });
```

Ship as `<SunfishVideoRoom mode="prebuilt">` in Bridge. Zero media UI code required.

### Pattern B — Headless Custom UI (full Sunfish integration)

```javascript
const call = Daily.createCallObject();
call.join({ url: roomUrl, token: meetingToken });
call.on('participant-joined', updateUI);
call.on('track-started', attachTrackToVideoElement);
```

Ship as `<SunfishVideoRoom mode="custom">` — full control over video tiles, layouts, controls, overlays. Use this for a Sunfish-styled meeting UI.

### Pattern C — React Adapter (`@daily-co/daily-react`)

Direct fit for `ui-adapters-react`. Wrap `<DailyProvider>` in a `SunfishDailyProvider` that also handles token fetching.

### Scheduling Integration (Bridge + Cal.diy)

No native integration between Daily and Cal.diy. Wire at the Bridge application layer:

1. Bridge server creates a Daily room with `nbf`/`exp` on calendar event creation
2. Bridge server mints per-attendee meeting tokens at join time (short-lived, via REST)
3. Bridge sends join links with tokens embedded
4. Daily room auto-expires when the calendar event ends

---

## Pricing Model

Per-participant-minute billing with volume discounts. Verify current tier pricing at `daily.co/pricing` — exact per-minute rates change over time. Key structure:
- **Free tier** — meaningful monthly free minutes allocation
- **Pay-as-you-go** — per participant-minute, automatic volume discounts
- Recording, transcription, noise cancellation may have per-minute add-on charges
- Large call mode (1,000+ participants) may require a higher tier

---

## Self-Hosting

**Not available.** Daily is fully hosted SaaS. No Docker image, Helm chart, or on-prem license.

**Implications for Bridge enterprise deployments:**
- All media routes through Daily's infrastructure
- Data residency handled via Daily's DPA (EU regional routing available)
- Hard blocker for strict on-prem requirements — consider LiveKit, Janus, or mediasoup for those cases

---

## Lessons for Sunfish Component Architecture

Daily's SDK design embodies several patterns worth adopting across Sunfish:

### A. Provider/Context as Root Boundary
`DailyProvider` is the single entry point owning the call object lifecycle. All child hooks consume context — no prop drilling.

**Apply in Sunfish:** `<SunfishVideoProvider>` owns the call object; child components are purely reactive consumers. This maps directly to Sunfish's existing composition model.

### B. Stable Identity via IDs, Not Array Index
All Daily APIs are keyed on `session_id` (UUID per join), not array position. `useParticipantIds()` returns ID arrays; components receive an ID and fetch their own data.

**Apply in Sunfish:** Key all participant/item lists by stable IDs. Avoid array-index keys in any list that can reorder or partially update — this is the root cause of React key-churn bugs.

### C. Explicit State Enums, Not Boolean Flags
Video/audio track state is an enum: `'blocked'`, `'off'`, `'sendable'`, `'loading'`, `'playable'`, `'interrupted'`. Not `isPlaying: true/false`.

**Apply in Sunfish:** Adopt explicit state enums for any async or multi-state component props (media, upload, connection status). Avoid `isX: bool` proliferation.

### D. Declarative Settings Object API
`call.updateInputSettings({ audio: { processor: { type: 'noise-cancellation' } }, video: { processor: { type: 'background-blur' } } })` — a single settings object rather than separate methods per feature.

**Apply in Sunfish:** For components with many configurable behaviors, prefer a single `Settings` object over N individual props. Reduces API surface and is composable.

### E. Event-Driven Architecture with Typed Events
All call state flows through a typed event system (`participant-joined`, `track-started`, `app-message`) rather than polling. The JS SDK fires events; the Blazor bridge invokes `DotNetObjectReference` callbacks.

**Apply in Sunfish:** Map JS SDK events 1:1 to `EventCallback<T>` parameters in Blazor components. Don't poll component state — subscribe to typed events.

### F. Server Owns Secrets, Client Gets Only Tokens
Daily's REST API is API-key protected (server-side only). Clients get short-lived meeting tokens. No secrets in the browser.

**Apply in Sunfish Bridge:** `IDailyRoomService` is a pure server-side C# service. The Blazor component receives only a `roomUrl` and a short-lived `token` — never an API key.

### G. `data-*` Attributes for CSS-Driven State
`<DailyVideo>` sets `data-playable`, `data-mirrored`, `data-local` on the `<video>` element. CSS handles show/hide of placeholders and mute overlays without JS.

**Apply in Sunfish:** Blazor Razor components should map component state to HTML `data-*` attributes, enabling pure CSS state styling in `.razor.css` files without additional JS event listeners.

---

## Integration Feasibility Matrix

| Criterion | Assessment | Notes |
|---|---|---|
| Blazor integration | ✅ Feasible | JS interop; well-defined pattern (server REST + client JS bridge) |
| React integration | ✅ First-class | `@daily-co/daily-react` — direct fit for ui-adapters-react |
| C# server-side | ✅ Clean | Pure REST API, trivial HttpClient wrapper |
| Daily Prebuilt (zero UI) | ✅ Excellent | iframe embed, no media tile code needed |
| Custom branded UI | ✅ Supported | `createCallObject()` headless path |
| Recording / Transcription | ✅ Production-grade | Deepgram transcription, multi-format recording |
| Enterprise features | ✅ Present | Noise cancel, background AI, large calls |
| Self-hosting | ❌ Not available | Hard blocker for strict on-prem requirements |
| Cal.diy integration | ⚠️ Manual | No native integration; wire at Bridge application layer |
| Breakout rooms | ⚠️ Prebuilt only | Not available in custom `createCallObject()` mode (beta) |

---

## Recommended Bridge Architecture

```
Bridge Server (C#)
  ├── IDailyRoomService        → POST /v1/rooms (create with nbf/exp)
  └── IDailyTokenService       → POST /v1/meeting-tokens (per-attendee, short-lived)

Bridge Client (Blazor WASM)
  ├── <SunfishVideoRoom mode="prebuilt">   → daily-js createFrame() → Daily Prebuilt iframe
  └── <SunfishVideoRoom mode="custom">    → daily-bridge.js → createCallObject()
                                            → DotNetObjectReference event callbacks
                                            → Razor <video id="tile-{sessionId}"> binding

Bridge Client (React, if applicable)
  └── <SunfishDailyProvider>              → @daily-co/daily-react DailyProvider wrapper
      └── <SunfishVideoTile>              → <DailyVideo sessionId={...} />
```

The two modes (`prebuilt` and `custom`) share the same server-side infrastructure. The `prebuilt` mode ships first (lowest effort); `custom` follows with the full Sunfish-styled UI.
