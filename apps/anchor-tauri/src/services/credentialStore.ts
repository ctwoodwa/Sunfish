// W#60 P4 PR 1 — Stronghold-backed credential store.
//
// Wraps `@tauri-apps/plugin-stronghold` with a small typed API for storing the
// Bridge auth token. The underlying Stronghold vault is encrypted by a 32-byte
// master key derived on the Rust side from the OS keychain (DPAPI on Windows,
// Keychain on macOS, Secret Service on Linux — see `src-tauri/src/lib.rs`).
//
// The `password` argument to `Stronghold.load()` is consumed by the Rust-side
// closure which IGNORES it (machine-locked OS-keychain derivation). The value
// here is a stable non-empty constant — it does not need to be secret and does
// not gate access to the vault.

import { Stronghold, type Client } from '@tauri-apps/plugin-stronghold'
import { appDataDir } from '@tauri-apps/api/path'

const SNAPSHOT_NAME = 'anchor.stronghold'
const CLIENT_NAME = 'anchor-auth'
const TOKEN_KEY = 'bridge-token'
// Ignored by the Rust closure; see file header.
const STRONGHOLD_INIT_PASSWORD = 'anchor-machine-locked'

interface CachedHandles {
  stronghold: Stronghold
  client: Client
}

// Module-level cache so we open the snapshot once per app lifetime.
// Tests reset this via `resetForTesting()`.
let cached: CachedHandles | null = null

async function snapshotPath(): Promise<string> {
  const dir = await appDataDir()
  return `${dir}/${SNAPSHOT_NAME}`
}

async function getHandles(): Promise<CachedHandles> {
  if (cached) return cached
  const path = await snapshotPath()
  const stronghold = await Stronghold.load(path, STRONGHOLD_INIT_PASSWORD)
  let client: Client
  try {
    client = await stronghold.loadClient(CLIENT_NAME)
  } catch {
    client = await stronghold.createClient(CLIENT_NAME)
  }
  cached = { stronghold, client }
  return cached
}

/** Initialize the credential store. Safe to call multiple times. */
export async function init(): Promise<void> {
  await getHandles()
}

/** Persist the Bridge auth token. Overwrites any existing value. */
export async function setToken(token: string): Promise<void> {
  const { stronghold, client } = await getHandles()
  const store = client.getStore()
  const data = Array.from(new TextEncoder().encode(token))
  await store.insert(TOKEN_KEY, data)
  await stronghold.save()
}

/** Returns the stored Bridge auth token, or `null` if not present. */
export async function getToken(): Promise<string | null> {
  const { client } = await getHandles()
  const store = client.getStore()
  try {
    const bytes = await store.get(TOKEN_KEY)
    if (!bytes || bytes.length === 0) return null
    return new TextDecoder().decode(new Uint8Array(bytes))
  } catch {
    return null
  }
}

/** Remove the stored Bridge auth token. Idempotent (no error if already absent). */
export async function clearToken(): Promise<void> {
  const { stronghold, client } = await getHandles()
  const store = client.getStore()
  try {
    await store.remove(TOKEN_KEY)
  } catch {
    // Already absent — idempotent.
    return
  }
  await stronghold.save()
}

/**
 * Test-only: clears the module-level cache so the next call re-runs the
 * `Stronghold.load()` / client-resolution path. Production code should never
 * call this — the cache is the correct steady-state.
 */
export function resetForTesting(): void {
  cached = null
}
