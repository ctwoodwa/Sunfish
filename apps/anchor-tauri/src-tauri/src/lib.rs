pub mod commands;
pub mod db;
pub mod sync;

use std::sync::Arc;

use rand::RngCore;
use tauri::Manager;

/// Service name used for the OS-keychain entry that backs Stronghold's master key.
/// Suffixed with `.stronghold` (council R7) to leave the bare app identifier free
/// for any future Tauri-internal keychain use (e.g., autoupdater token storage).
const KEYRING_SERVICE: &str = "io.sunfish.anchor.stronghold";
/// Account name within the keychain service. A single Anchor install holds one
/// Stronghold master key; if we ever support multi-profile installs this becomes
/// `stronghold-master-key:<profile-id>` keyed off the active profile.
const KEYRING_USER: &str = "stronghold-master-key";
/// 32 bytes (256 bits) — matches Stronghold's expected key length for AEAD.
const STRONGHOLD_KEY_LEN: usize = 32;
/// Sentinel key returned by the Stronghold password closure when the keychain
/// derivation failed at setup time. Stronghold will fail to decrypt any existing
/// snapshot with this value; the JS side checks `keychain_status` before calling
/// `Stronghold.load()`, so this branch is only reached as a defense-in-depth
/// against ordering bugs (and is preferable to panicking the Tauri runtime —
/// council A1.4).
const STRONGHOLD_KEY_SENTINEL: [u8; STRONGHOLD_KEY_LEN] = [0u8; STRONGHOLD_KEY_LEN];

/// Derives the Stronghold master key from the OS keychain. On first launch this
/// generates a fresh 32-byte random key via `OsRng` (Windows: BCryptGenRandom;
/// macOS: getentropy/SecRandomCopyBytes; Linux: getrandom(2) — council R6) and
/// persists it in the platform credential store (Windows Credential Manager via
/// DPAPI, macOS Keychain, Linux Secret Service via libsecret). On subsequent
/// launches the stored key is returned verbatim.
///
/// The key is machine-locked: it is gated by the user's OS login session and is
/// not portable across machines. Anchor's auth-token vault is therefore tied to
/// the user's Windows/macOS/Linux account on the device that created it.
///
/// W#60 P4 PR 1 — replaces the Phase 3 stub KDF (which returned
/// `password.as_bytes()` under a `compile_error!` guard). Council A1.1 requires
/// the platform-native keyring features in Cargo.toml; A1.4 moved this call
/// from inside the Stronghold password closure to setup time so failures
/// surface via the `keychain_status` command instead of panicking the runtime.
fn derive_stronghold_master_key() -> Result<Vec<u8>, String> {
    let entry = keyring::Entry::new(KEYRING_SERVICE, KEYRING_USER)
        .map_err(|e| format!("keyring entry init: {e}"))?;

    match entry.get_secret() {
        Ok(bytes) if bytes.len() == STRONGHOLD_KEY_LEN => Ok(bytes),
        Ok(other) => {
            // Length mismatch — refuse to silently regenerate (could indicate
            // tampering or a partially-written entry). Surface for investigation.
            Err(format!(
                "keyring entry has unexpected length: got {}, expected {}",
                other.len(),
                STRONGHOLD_KEY_LEN
            ))
        }
        Err(_) => {
            // No entry yet (first launch) — generate + persist.
            let mut key = vec![0u8; STRONGHOLD_KEY_LEN];
            rand::rngs::OsRng.fill_bytes(&mut key);
            entry
                .set_secret(&key)
                .map_err(|e| format!("keyring set_secret: {e}"))?;
            Ok(key)
        }
    }
}

pub fn run() {
    // W#60 P4 PR 1 council A1.4 — derive the Stronghold master key ONCE at
    // process start, outside the plugin closure. Failure here is surfaced via
    // the `keychain_status` Tauri command so the JS AuthGate can render a
    // diagnostic banner instead of the Tauri process panicking with a vanished
    // window. The Arc is cloned into the Stronghold builder closure so the
    // closure body is panic-free; if the JS side incorrectly calls
    // `Stronghold.load()` despite a non-Ok status, the closure returns a
    // sentinel zero-key and Stronghold's normal decryption-error path runs.
    let derived = derive_stronghold_master_key();
    let derive_status = derived.as_ref().map(|_| ()).map_err(|e| e.clone());
    let key_arc: Arc<Result<Vec<u8>, String>> = Arc::new(derived);
    let key_arc_for_closure = Arc::clone(&key_arc);

    tauri::Builder::default()
        // Single-instance must be registered FIRST per the plugin docs — it has
        // to intercept the second-launch IPC before any other plugin spins up.
        // Without this, every Start Menu / taskbar / .lnk click on Anchor spawns
        // a fresh process + webview, so a leftover instance gives the user two
        // (or more) Anchor windows. On second launch this callback fires inside
        // the EXISTING process and just refocuses the primary window.
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.show();
                let _ = window.set_focus();
            }
        }))
        .plugin(tauri_plugin_shell::init())
        .plugin(
            tauri_plugin_stronghold::Builder::new(move |_password_from_js| {
                // Closure is now panic-free. The OS-keychain key was derived at
                // setup time above; we just clone it out of the Arc. If derive
                // failed, the JS side should have rendered the keychain banner
                // (per `keychain_status` command) before getting here; the
                // sentinel path is defense-in-depth.
                match &*key_arc_for_closure {
                    Ok(bytes) => bytes.clone(),
                    Err(_) => STRONGHOLD_KEY_SENTINEL.to_vec(),
                }
            })
            .build(),
        )
        .setup(move |app| {
            let data_dir = app.path().app_data_dir()?;
            let pool = tauri::async_runtime::block_on(db::open(&data_dir))
                .map_err(|e| format!("db init: {e}"))?;

            let bridge_url = std::env::var("BRIDGE_URL")
                .unwrap_or_else(|_| "http://localhost:7080".to_string());
            let auth_token = std::env::var("BRIDGE_TOKEN").unwrap_or_default();

            // Validate BRIDGE_URL: scheme must be http/https; host must be loopback
            // unless ANCHOR_BRIDGE_ALLOW_REMOTE=1 is set (for future remote deployments).
            {
                let parsed = url::Url::parse(&bridge_url)
                    .map_err(|e| format!("BRIDGE_URL parse error: {e}"))?;
                match parsed.scheme() {
                    "http" | "https" => {}
                    s => return Err(format!("BRIDGE_URL scheme must be http or https, got {s}").into()),
                }
                if let Some(host) = parsed.host_str() {
                    let loopback =
                        host == "localhost" || host == "127.0.0.1" || host == "::1";
                    let allow_remote = std::env::var("ANCHOR_BRIDGE_ALLOW_REMOTE")
                        .ok()
                        .as_deref()
                        == Some("1");
                    if !loopback && !allow_remote {
                        return Err(format!(
                            "BRIDGE_URL host {host} is non-loopback; \
                             set ANCHOR_BRIDGE_ALLOW_REMOTE=1 to override"
                        )
                        .into());
                    }
                }
            }

            // W#60 P4 PR 1 — Bridge auth token state.
            //
            // Initial value comes from BRIDGE_TOKEN env var (dev/CI fallback);
            // JS updates it via `set_bridge_token` after reading from Stronghold
            // or after a successful LoginPage submit. The sync tasks below clone
            // this Arc and read the current value on every HTTP call (council R4)
            // so token rotation takes effect without re-spawning the tasks.
            let auth_state = commands::auth::AuthToken::new(auth_token);
            let auth_arc = Arc::clone(&auth_state.0);
            app.manage(auth_state);

            // Bridge URL state. Exposes the active base URL to JS via
            // `get_bridge_url` so the LoginPage A1.3 probe uses an absolute URL
            // — relative `/api/v1/whoami` works only via the Vite proxy in
            // `tauri:dev`; the bundled build's Tauri asset handler returns 200
            // (SPA fallback) for unknown paths, making the relative probe a
            // false positive. Caught by manual smoke test 2026-05-16.
            app.manage(commands::auth::BridgeUrl(bridge_url.clone()));

            // Background pull sync on startup. Reads token fresh per HTTP call.
            let pool_clone = pool.clone();
            let bridge_url_clone = bridge_url.clone();
            let auth_arc_pull = Arc::clone(&auth_arc);
            tauri::async_runtime::spawn(async move {
                if let Err(e) =
                    sync::pull::pull_all(&pool_clone, &bridge_url_clone, &auth_arc_pull).await
                {
                    eprintln!("[sync] startup pull failed: {e}");
                }
            });

            // Drain any pending write-queue entries from previous offline sessions.
            let pool_push = pool.clone();
            let bridge_url_push = bridge_url.clone();
            let auth_arc_push = Arc::clone(&auth_arc);
            tauri::async_runtime::spawn(async move {
                if let Err(e) = sync::push::drain_write_queue(
                    &pool_push,
                    &bridge_url_push,
                    &auth_arc_push,
                )
                .await
                {
                    eprintln!("[sync] startup write-queue drain failed: {e}");
                }
            });

            // W#60 P4 PR 1 council A1.4 — register the keychain derivation
            // outcome so the JS AuthGate can render a precise banner if the
            // OS keychain is unavailable, instead of opaque Stronghold errors.
            app.manage(commands::auth::KeychainStatus::new(derive_status.clone()));
            app.manage(pool);
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            commands::auth::set_bridge_token,
            commands::auth::has_bridge_token,
            commands::auth::keychain_status,
            commands::auth::get_bridge_url,
            commands::cache::get_cached_properties,
            commands::cache::get_cached_leases,
            commands::cache::get_cached_payments,
            commands::cache::get_cached_maintenance_tickets,
            commands::write_queue::enqueue_write,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
