# W#60 Phase 3 — Tauri + Offline + Loro CRDT
## Stage 06 Build Hand-off

**Workstream:** W#60 — ERPNext Composition Pivot  
**Phase:** 3 of 5 (Tauri v2 + offline SQLite cache + write queue + Loro CRDT)  
**Owner:** sunfish-PM (COB)  
**ADR gate:** ADR 0086 `Accepted` (PR #737 — currently `Proposed`; **DO NOT START until PR #737 merges with `status: Accepted`**)  
**Estimated effort:** ~3 dev-weeks (~8–12h focused sessions)  
**PR count:** 3 PRs

---

## Context

Phase 2 shipped `apps/anchor-react/` — a Vite + React 19 + TypeScript standalone SPA calling Bridge over HTTP. All 6 property-management screens work in browser. `@sunfish/ui-react` package ships `SyncStateBadge`, `OfflineIndicator`, `PropertyCard`, `RoleGate`, `CompanySwitcher`.

Phase 3 wraps that React app in a Tauri v2 native shell, adds a SQLite offline read cache, and introduces an offline write queue + Loro CRDT for AP-class data (maintenance notes/comments). When Phase 3 ships, `apps/anchor-react/` is retired — the `src/` tree moves under `apps/anchor-tauri/src/`.

**Critical boundary:** CP-class writes (rent payments, journal entries) are **refused when offline** — the UI disables submit and shows a "Requires network connection" callout. AP-class writes (maintenance notes, photos, maintenance comments) are **queued offline** via write queue + Loro CRDT and synced on reconnect.

---

## Pre-build checklist

Before writing any code:

1. Confirm PR #737 (ADR 0086) has merged with `status: Accepted`.
2. Run `git log --oneline main -5` — verify no parallel Phase 3 PRs already landed.
3. Run `gh pr list --state open` — look for any W#60 Phase 3 PRs already in flight.
4. Verify `apps/anchor-react/` is on `main` and its `src/` directory is the current Phase 2 output (last PR: #758).
5. Confirm Surface Pro is available for CO to test ARM Windows builds (Phase 3 PASS requires CO verification on device).

---

## PR 1 — Tauri shell scaffold (`apps/anchor-tauri/`)

**Branch:** `w60/phase3-pr1-tauri-scaffold`  
**Acceptance gate:** `npm run tauri dev` opens all 6 Phase 2 screens in a native window; `npm run tauri build` produces a Windows `.msi` installer ≤50 MB.

### What to build

#### 1. Create `apps/anchor-tauri/` directory structure

```
apps/anchor-tauri/
├── src-tauri/
│   ├── Cargo.toml
│   ├── build.rs
│   ├── tauri.conf.json
│   └── src/
│       ├── main.rs
│       └── lib.rs
├── src/                     ← absorb from apps/anchor-react/src/ verbatim
│   └── (all Phase 2 files)
├── index.html               ← copy from apps/anchor-react/index.html
├── package.json
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.node.json
├── vite.config.ts
├── vitest.config.ts
└── eslint.config.js
```

#### 2. `src-tauri/Cargo.toml`

```toml
[package]
name = "anchor-tauri"
version = "0.1.0"
edition = "2021"

[build-dependencies]
tauri-build = { version = "2", features = [] }

[dependencies]
tauri = { version = "2", features = ["protocol-asset"] }
tauri-plugin-sql = { version = "2", features = ["sqlite"] }
tauri-plugin-stronghold = "2"
tauri-plugin-shell = "2"
serde = { version = "1", features = ["derive"] }
serde_json = "1"
tokio = { version = "1", features = ["full"] }

[profile.release]
strip = true
opt-level = "z"
lto = true
codegen-units = 1
panic = "abort"
```

#### 3. `src-tauri/tauri.conf.json`

```json
{
  "$schema": "https://schema.tauri.app/config/2.0.0",
  "productName": "Anchor",
  "version": "0.1.0",
  "identifier": "io.sunfish.anchor",
  "build": {
    "frontendDist": "../dist",
    "devUrl": "http://localhost:1420",
    "beforeDevCommand": "npm run dev",
    "beforeBuildCommand": "npm run build"
  },
  "app": {
    "windows": [
      {
        "title": "Anchor",
        "width": 1280,
        "height": 800,
        "minWidth": 960,
        "minHeight": 600
      }
    ],
    "security": {
      "csp": "default-src 'self' tauri: asset: http://localhost:7080; connect-src 'self' http://localhost:7080 ws://localhost:7080; script-src 'self'; style-src 'self' 'unsafe-inline'"
    }
  },
  "bundle": {
    "active": true,
    "targets": "all",
    "icon": ["icons/32x32.png", "icons/128x128.png", "icons/icon.icns", "icons/icon.ico"]
  }
}
```

> The `connect-src` allows fetch calls to Bridge on `localhost:7080`. Adjust if Bridge runs on a different port. Direct ERPNext calls from the renderer are **not** permitted — all data flows through Bridge.

#### 4. `src-tauri/src/main.rs`

```rust
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    anchor_tauri_lib::run();
}
```

#### 5. `src-tauri/src/lib.rs`

```rust
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_sql::Builder::default().build())
        .plugin(tauri_plugin_stronghold::Builder::new(|password| {
            // Phase 3: stronghold key derivation from device identifier
            // Phase 4: integrate OS keychain here
            password.as_bytes().to_vec()
        }).build())
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
```

#### 6. `package.json` (differs from `anchor-react/package.json`)

Copy `apps/anchor-react/package.json` and add:

```json
{
  "name": "@sunfish/anchor-tauri",
  "scripts": {
    "dev": "vite --port 1420",
    "tauri": "tauri",
    "tauri:dev": "tauri dev",
    "tauri:build": "tauri build"
  },
  "dependencies": {
    "@tauri-apps/api": "^2.0.0",
    "@tauri-apps/plugin-shell": "^2.0.0",
    "@tauri-apps/plugin-sql": "^2.0.0"
  },
  "devDependencies": {
    "@tauri-apps/cli": "^2.0.0"
  }
}
```

Keep all existing `anchor-react` deps unchanged.

#### 7. `vite.config.ts`

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const host = process.env.TAURI_DEV_HOST

export default defineConfig({
  plugins: [react(), tailwindcss()],
  clearScreen: false,
  server: {
    port: 1420,
    strictPort: true,
    host: host || false,
    hmr: host ? { protocol: 'ws', host, port: 1421 } : undefined,
    proxy: {
      '/api': 'http://localhost:7080',
      '/hubs': { target: 'http://localhost:7080', ws: true },
    },
  },
  envPrefix: ['VITE_', 'TAURI_'],
  build: {
    target: process.env.TAURI_ENV_PLATFORM === 'windows' ? 'chrome105' : 'safari13',
    minify: !process.env.TAURI_ENV_DEBUG ? 'esbuild' : false,
    sourcemap: !!process.env.TAURI_ENV_DEBUG,
  },
})
```

#### 8. `src/` — absorb Phase 2 files

Copy all files from `apps/anchor-react/src/` verbatim. No behavior changes in PR 1 — the goal is "Phase 2 screens working in a Tauri window."

#### 9. Add `apps/anchor-tauri/` to repo root workspaces

If `package.json` at repo root has a `workspaces` field, add `"apps/anchor-tauri"`.

#### 10. Update `.wolf/anatomy.md` + `active-workstreams.md`

Add `apps/anchor-tauri/` entries to anatomy.md. Update W#60 status_cell to reference Phase 3 PR 1 in progress.

### Tests for PR 1

- All Phase 2 Vitest tests pass inside `apps/anchor-tauri/` (copy `LeasesPage.test.tsx` and `PropertiesPage.test.tsx`)
- `npm run tauri build` CI step produces an artifact (add to GitHub Actions workflow for Windows runner)
- No new unit tests required — functional parity with Phase 2 is the gate

### What NOT to do in PR 1

- Do not add SQLite or stronghold logic yet (PR 2)
- Do not modify any `src/api/erpnext.ts` calls yet — all requests still go to Bridge via `fetch()`
- Do not retire `apps/anchor-react/` yet — that happens in PR 3 (cleanup PR)

---

## PR 2 — SQLite offline read cache + sync status

**Branch:** `w60/phase3-pr2-sqlite-cache`  
**Depends on:** PR 1 merged  
**Acceptance gate:** CO closes WiFi on Surface Pro → opens Anchor → Properties/Leases/Maintenance load from cache → `SyncStateBadge` shows amber (stale) or green (fresh).

### What to build

#### 1. SQLite schema (`src-tauri/src/db/schema.sql`)

```sql
CREATE TABLE IF NOT EXISTS properties (
    name        TEXT PRIMARY KEY,
    data_json   TEXT NOT NULL,
    company     TEXT NOT NULL,
    synced_at   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS leases (
    name        TEXT PRIMARY KEY,
    data_json   TEXT NOT NULL,
    company     TEXT NOT NULL,
    synced_at   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS payments (
    name        TEXT PRIMARY KEY,
    data_json   TEXT NOT NULL,
    lease       TEXT NOT NULL,
    synced_at   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS maintenance_tickets (
    name        TEXT PRIMARY KEY,
    data_json   TEXT NOT NULL,
    company     TEXT NOT NULL,
    synced_at   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sync_metadata (
    table_name  TEXT PRIMARY KEY,
    last_synced TEXT NOT NULL,
    record_count INTEGER NOT NULL DEFAULT 0
);
```

#### 2. Tauri commands (`src-tauri/src/commands/cache.rs`)

```rust
#[tauri::command]
pub async fn get_cached_properties(
    db: tauri::State<'_, tauri_plugin_sql::DbPool>,
) -> Result<Vec<serde_json::Value>, String> {
    let rows = sqlx::query("SELECT data_json FROM properties")
        .fetch_all(db.inner())
        .await
        .map_err(|e| e.to_string())?;
    rows.iter()
        .map(|r| serde_json::from_str::<serde_json::Value>(r.get("data_json")))
        .collect::<Result<_, _>>()
        .map_err(|e| e.to_string())
}

// Identical commands for: get_cached_leases, get_cached_payments, get_cached_maintenance_tickets
// Pattern: SELECT data_json FROM {table}
```

#### 3. Pull sync (`src-tauri/src/sync/pull.rs`)

On app startup and on network reconnect (listen for Tauri `network-status-change` event):

```rust
pub async fn pull_all(
    db: &tauri_plugin_sql::DbPool,
    bridge_base_url: &str,
    auth_token: &str,
) -> anyhow::Result<()> {
    pull_table(db, bridge_base_url, auth_token, "properties",
               "/api/v1/erpnext/properties").await?;
    pull_table(db, bridge_base_url, auth_token, "leases",
               "/api/v1/erpnext/leases").await?;
    pull_table(db, bridge_base_url, auth_token, "maintenance_tickets",
               "/api/v1/erpnext/maintenance").await?;
    // payments: last 90 days only (limit=200 guard)
    pull_table(db, bridge_base_url, auth_token, "payments",
               "/api/v1/erpnext/payments?limit=200").await?;
    Ok(())
}
```

`pull_table` fetches from Bridge, upserts into SQLite via `INSERT OR REPLACE`, updates `sync_metadata`. Auth token comes from stronghold (Phase 3 stub: read from `tauri.conf.json` `env` for dev; Phase 4: stronghold).

#### 4. `src/stores/syncStore.ts` (new)

```typescript
import { create } from 'zustand'

export type SyncState = 'syncing' | 'fresh' | 'stale' | 'offline'

interface SyncStoreState {
  syncState: SyncState
  lastSyncedAt: Date | null
  setSyncState: (s: SyncState) => void
  setLastSyncedAt: (d: Date) => void
}

export const useSyncStore = create<SyncStoreState>()((set) => ({
  syncState: 'stale',
  lastSyncedAt: null,
  setSyncState: (syncState) => set({ syncState }),
  setLastSyncedAt: (lastSyncedAt) => set({ lastSyncedAt }),
}))
```

#### 5. `src/hooks/usePropertiesOffline.ts` (replaces `useProperties.ts` for offline support)

```typescript
import { useQuery } from '@tanstack/react-query'
import { invoke } from '@tauri-apps/api/core'
import { getProperties } from '../api/erpnext'

const IS_TAURI = '__TAURI_INTERNALS__' in window

export function useProperties() {
  return useQuery({
    queryKey: ['properties'],
    queryFn: async () => {
      if (IS_TAURI) {
        try {
          // Try live fetch first; on failure fall through to cache
          const live = await getProperties()
          return live
        } catch {
          return invoke<Property[]>('get_cached_properties')
        }
      }
      return getProperties()
    },
    staleTime: 60_000,
  })
}
```

Apply the same pattern to `useLeases.ts`, creating `useMaintenanceOffline.ts` variants. The `IS_TAURI` guard ensures `apps/anchor-react/` tests continue to pass without a Tauri runtime.

#### 6. Wire `SyncStateBadge` from `@sunfish/ui-react`

In `src/app.tsx` header/nav area:

```typescript
import { SyncStateBadge } from '@sunfish/ui-react'
import { useSyncStore } from './stores/syncStore'

// In component:
const { syncState } = useSyncStore()
return <SyncStateBadge state={syncState} />
```

`SyncStateBadge` maps: `fresh` → green dot, `stale` → amber dot + last-synced timestamp, `offline` → red dot + "Offline", `syncing` → spinner.

#### 7. `OfflineBanner` in layout

In the root layout (wrap all pages):

```typescript
import { OfflineBanner } from '@sunfish/ui-react'
import { useSyncStore } from './stores/syncStore'

const { syncState } = useSyncStore()
return (
  <>
    {syncState === 'offline' && <OfflineBanner />}
    {children}
  </>
)
```

### Tests for PR 2

- Unit test (Vitest): `useProperties` hook — mock `invoke` to return fixture data; assert it returns when `IS_TAURI = true` and fetch throws
- Integration: Playwright E2E test with `VITE_OFFLINE_MOCK=true` env — Properties page renders from stub cache data
- Manual CO test on Surface Pro (ARM Windows): disconnect WiFi → launch Anchor → confirm Properties and Leases load

---

## PR 3 — Offline write queue + Loro CRDT (AP-class) + cleanup

**Branch:** `w60/phase3-pr3-write-queue-loro`  
**Depends on:** PR 2 merged  
**Security council required:** yes (write queue + stronghold handle device credentials)  
**Acceptance gate:** CO offline on Surface Pro: adds a maintenance note → reconnects → note appears in ERPNext ≤10s after reconnect.

### What to build

#### 1. Write queue table (add to schema)

```sql
CREATE TABLE IF NOT EXISTS write_queue (
    id          TEXT PRIMARY KEY,        -- UUID
    doctype     TEXT NOT NULL,           -- "Maintenance Note"
    op_type     TEXT NOT NULL,           -- "create" | "update"
    doc_name    TEXT,                    -- null for creates
    payload_json TEXT NOT NULL,
    created_at  TEXT NOT NULL,
    synced_at   TEXT,                    -- null until synced
    error       TEXT                     -- set on sync failure
);
```

#### 2. `src-tauri/src/sync/push.rs` — drain write queue on reconnect

```rust
pub async fn drain_write_queue(
    db: &tauri_plugin_sql::DbPool,
    bridge_base_url: &str,
    auth_token: &str,
) -> anyhow::Result<()> {
    let pending = sqlx::query(
        "SELECT id, doctype, op_type, doc_name, payload_json FROM write_queue
         WHERE synced_at IS NULL AND error IS NULL
         ORDER BY created_at ASC"
    ).fetch_all(db.inner()).await?;

    for row in pending {
        let result = sync_one_entry(&row, bridge_base_url, auth_token).await;
        match result {
            Ok(_) => {
                sqlx::query(
                    "UPDATE write_queue SET synced_at = datetime('now') WHERE id = ?"
                ).bind(row.get::<&str, _>("id"))
                 .execute(db.inner()).await?;
            }
            Err(e) => {
                sqlx::query(
                    "UPDATE write_queue SET error = ? WHERE id = ?"
                ).bind(e.to_string())
                 .bind(row.get::<&str, _>("id"))
                 .execute(db.inner()).await?;
                // Log but don't halt the drain — try remaining entries
            }
        }
    }
    Ok(())
}
```

The `sync_one_entry` function maps `doctype` + `op_type` to the correct Bridge endpoint (`POST /api/v1/erpnext/maintenance` for new tickets, `PATCH /api/v1/erpnext/maintenance/{name}` for updates).

#### 3. Tauri command: `enqueue_write`

```rust
#[tauri::command]
pub async fn enqueue_write(
    db: tauri::State<'_, tauri_plugin_sql::DbPool>,
    doctype: String,
    op_type: String,
    doc_name: Option<String>,
    payload_json: String,
) -> Result<String, String> {
    let id = uuid::Uuid::new_v4().to_string();
    sqlx::query(
        "INSERT INTO write_queue (id, doctype, op_type, doc_name, payload_json, created_at)
         VALUES (?, ?, ?, ?, ?, datetime('now'))"
    )
    .bind(&id).bind(&doctype).bind(&op_type).bind(&doc_name).bind(&payload_json)
    .execute(db.inner())
    .await
    .map_err(|e| e.to_string())?;
    Ok(id)
}
```

Add `uuid = "1"` to `Cargo.toml` dependencies.

#### 4. AP-class: Loro CRDT for maintenance notes

Maintenance ticket notes (free-text field, concurrent editing possible between CO and accountant in Phase 4) use Loro CRDT:

**npm side** (`package.json`):
```json
"loro-crdt": "^1.0.0"
```

**`src/lib/loro.ts`** (new):
```typescript
import { Loro, LoroText } from 'loro-crdt'

const docs = new Map<string, Loro>()

export function getLoroDoc(ticketName: string): Loro {
  if (!docs.has(ticketName)) {
    const doc = new Loro()
    docs.set(ticketName, doc)
  }
  return docs.get(ticketName)!
}

export function getNoteText(ticketName: string): LoroText {
  return getLoroDoc(ticketName).getText('notes')
}

export function exportUpdate(ticketName: string): Uint8Array {
  return getLoroDoc(ticketName).exportSnapshot()
}

export function importUpdate(ticketName: string, update: Uint8Array): void {
  getLoroDoc(ticketName).import(update)
}
```

**`src/hooks/useMaintenanceNoteOffline.ts`** (new):
```typescript
import { invoke } from '@tauri-apps/api/core'
import { getNoteText, exportUpdate } from '../lib/loro'

const IS_TAURI = '__TAURI_INTERNALS__' in window

export function useMaintenanceNoteSubmit(ticketName: string) {
  const { syncState } = useSyncStore()

  return async (noteText: string) => {
    if (IS_TAURI && syncState === 'offline') {
      // AP-class: queue the write
      const text = getNoteText(ticketName)
      text.insert(0, noteText)
      const update = exportUpdate(ticketName)
      await invoke('enqueue_write', {
        doctype: 'Maintenance Note',
        opType: 'create',
        docName: null,
        payloadJson: JSON.stringify({
          ticket: ticketName,
          content: noteText,
          loro_snapshot: Array.from(update),
        }),
      })
    } else {
      // Online: post directly to Bridge
      await createMaintenanceTicket({ Subject: noteText, Property: '', Priority: 'Medium' })
    }
  }
}
```

> **AP vs CP boundary is enforced here.** Notes are AP-class (Loro + queue). Payment submissions are CP-class — see next section.

#### 5. CP-class: refuse rent payment submission when offline

In `src/pages/RentCollectionPage.tsx`, add:

```typescript
const { syncState } = useSyncStore()
const isOffline = syncState === 'offline'

// On the submit button:
<Button disabled={isOffline} type="submit">
  {isOffline ? 'Network required for payments' : 'Record Payment'}
</Button>
{isOffline && (
  <p className="text-sm text-amber-600">
    Rent payments require a live connection to ERPNext to maintain ledger integrity.
  </p>
)}
```

Same pattern for `AccountingPage.tsx` (journal entry submission disabled offline).

#### 6. Cleanup: retire `apps/anchor-react/`

In the same PR (or a follow-up cleanup PR if preferred by COB):

```
# In apps/anchor-react/package.json — add deprecation note in description:
"description": "RETIRED in W#60 Phase 3 — use apps/anchor-tauri instead"

# Add RETIRED.md at apps/anchor-react/RETIRED.md:
```

```markdown
# Retired

This app was the pre-Tauri standalone React SPA (W#60 Phase 2).

Phase 3 absorbed all `src/` files into `apps/anchor-tauri/src/` and added
the Tauri native shell, SQLite cache, and Loro CRDT layer.

Do not install or run this app. Use `apps/anchor-tauri` instead.
```

Do **not** delete the directory — leave `apps/anchor-react/` as a reference for the Phase 2 git history. COB can mark it deleted in a follow-up chore PR after Phase 3 merges.

#### 7. Security council (mandatory before PR 3 merge)

Before merging PR 3, COB runs a council review of the write queue + Loro integration. Council must assess:

- Write queue drain: can a malicious Loro snapshot from a compromised peer overwrite valid data? (Phase 3 is CO-only so answer is "no," but Phase 4 accountant peer sync changes this — confirm Phase 3 is safe in isolation)
- `enqueue_write` Tauri command: SQL injection via `payload_json`? (Use parameterized queries — the schema above already uses `?` binding)
- Stronghold stub: dev path uses a fixed password string — is this acceptable for Phase 3 builds? (Yes for dev; note it in Phase 4 stronghold hand-off)

### Tests for PR 3

- Unit test: `enqueue_write` Tauri command inserts a row and returns a UUID (Rust `#[cfg(test)]` + in-memory SQLite)
- Unit test: `drain_write_queue` — mock Bridge endpoint, assert rows get `synced_at` set
- Vitest: `useMaintenanceNoteSubmit` with `IS_TAURI = true` and `syncState = 'offline'` — assert `invoke('enqueue_write')` is called, NOT the live Bridge fetch
- Vitest: `RentCollectionPage` with `syncState = 'offline'` — assert submit button is disabled
- Manual CO test on Surface Pro ARM Windows (required for Phase 3 PASS)

---

## Phase 3 PASS criteria (binding per ADR 0086 §Phase 3 PASS re-evaluation)

CO must personally verify each item on Surface Pro ARM Windows before Phase 3 is declared PASS:

| # | Criterion | Verified by |
|---|---|---|
| 1 | Anchor installs and runs from `.msi` installer | CO on Surface Pro |
| 2 | Cold start ≤3s from installer launch to Properties screen visible | CO — stopwatch |
| 3 | `.msi` installer file size ≤50 MB | CI artifact size check |
| 4 | Disconnect WiFi → all 6 screens load from SQLite cache | CO on Surface Pro |
| 5 | Offline: add maintenance note → reconnect → note in ERPNext ≤10s | CO on Surface Pro |
| 6 | Offline: attempt rent payment → submit button disabled with message | CO on Surface Pro |
| 7 | `SyncStateBadge` shows correct state (green/amber/offline) at each transition | CO observation |

---

## FAILED triggers (per ADR 0086 + W#60 UPF)

| Trigger | Fallback |
|---|---|
| Tauri v2 ARM Windows build fails after 1 engineering-week of effort | Browser PWA: add `manifest.json` + service worker to `apps/anchor-react/`; defer Tauri shell to Phase 5 |
| `loro-crdt` npm bindings incompatible with Tauri WebView | Automerge-ts: pre-approved ADR 0028 alternative; API surface is compatible; swap `loro-crdt` for `@automerge/automerge` |
| Bundle exceeds 50 MB after strip/LTO | Audit Rust deps; defer Loro from installer (load lazily); consider splitting WASM from main bundle |
| Any Phase 3 session exceeds 4 calendar weeks elapsed | Halt; flag to XO; re-evaluate scope |

---

## What COB must NOT do

- Do not call ERPNext directly from the React renderer — all HTTP calls go through Bridge.
- Do not implement bidirectional peer sync with the accountant in Phase 3 (that is Phase 4).
- Do not add stronghold-backed OS keychain storage yet — Phase 3 dev path uses `appsettings.Development.json` auth token forwarded by Bridge; stronghold integration is Phase 4.
- Do not retire `apps/anchor-react/` by deleting it — leave it with a `RETIRED.md` notice.
- Do not change any Bridge (`accelerators/bridge/`) code in Phase 3 — all Bridge endpoints are already built in Phase 2.

---

## References

- **ADR 0086** — `docs/adrs/0086-anchor-tauri-react-product-surface.md` (must be `Accepted` before start)
- **Phase 2 React API client** — `apps/anchor-react/src/api/erpnext.ts` (all endpoints built; copy verbatim)
- **`@sunfish/ui-react`** — `packages/ui-react/src/` (SyncStateBadge, OfflineIndicator, PropertyCard already built in Phase 2)
- **Bridge ERPNext proxy** — `accelerators/bridge/Sunfish.Bridge/Proxy/` (IERPNextClient, ERPNextHttpClient, ERPNextProxy)
- **Tauri v2 docs** — https://v2.tauri.app/start/
- **loro-crdt npm** — https://www.npmjs.com/package/loro-crdt
- **CP/AP boundary rationale** — `_shared/product/local-node-architecture-paper.md` §8 (write classification)
- **W#60 UPF plan** — `~/.claude/plans/noble-crunching-hopper.md`
