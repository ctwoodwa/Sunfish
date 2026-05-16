pub mod commands;
pub mod db;
pub mod sync;

use rand::RngCore;
use tauri::Manager;

/// Service name used for the OS-keychain entry that backs Stronghold's master key.
/// Must match the Tauri app identifier so the keychain entry is scoped per-app.
const KEYRING_SERVICE: &str = "io.sunfish.anchor";
/// Account name within the keychain service. A single Anchor install holds one
/// Stronghold master key; if we ever support multi-profile installs this becomes
/// `stronghold-master-key:<profile-id>` keyed off the active profile.
const KEYRING_USER: &str = "stronghold-master-key";
/// 32 bytes (256 bits) — matches Stronghold's expected key length for AEAD.
const STRONGHOLD_KEY_LEN: usize = 32;

/// Derives the Stronghold master key from the OS keychain. On first launch this
/// generates a fresh 32-byte random key via `OsRng` and persists it in the platform
/// credential store (Windows Credential Manager via DPAPI, macOS Keychain, Linux
/// Secret Service). On subsequent launches the stored key is returned verbatim.
///
/// The key is machine-locked: it is gated by the user's OS login session and is
/// not portable across machines. Anchor's auth-token vault is therefore tied to
/// the user's Windows/macOS/Linux account on the device that created it.
///
/// W#60 P4 PR 1 — replaces the Phase 3 stub KDF (which returned `password.as_bytes()`
/// and was guarded by a `compile_error!` blocking release builds).
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
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(
            tauri_plugin_stronghold::Builder::new(|_password_from_js| {
                // The password argument passed from the JS invoke is intentionally
                // ignored. Stronghold's encryption key is machine-locked via the OS
                // keychain (DPAPI on Windows, Keychain on macOS, Secret Service on
                // Linux), gated by the user's OS login session.
                //
                // Failure here is unrecoverable — without a derived key we cannot
                // open the Stronghold vault and the app cannot proceed. Panicking
                // with a clear message surfaces this as an OS-level startup error
                // rather than a silent auth failure later.
                derive_stronghold_master_key()
                    .expect("stronghold master-key derivation from OS keychain failed")
            })
            .build(),
        )
        .setup(|app| {
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

            // Background pull sync on startup
            let pool_clone = pool.clone();
            let bridge_url_clone = bridge_url.clone();
            let auth_token_clone = auth_token.clone();
            tauri::async_runtime::spawn(async move {
                if let Err(e) =
                    sync::pull::pull_all(&pool_clone, &bridge_url_clone, &auth_token_clone).await
                {
                    eprintln!("[sync] startup pull failed: {e}");
                }
            });

            // Drain any pending write-queue entries from previous offline sessions
            let pool_push = pool.clone();
            tauri::async_runtime::spawn(async move {
                if let Err(e) =
                    sync::push::drain_write_queue(&pool_push, &bridge_url, &auth_token).await
                {
                    eprintln!("[sync] startup write-queue drain failed: {e}");
                }
            });

            app.manage(pool);
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            commands::cache::get_cached_properties,
            commands::cache::get_cached_leases,
            commands::cache::get_cached_payments,
            commands::cache::get_cached_maintenance_tickets,
            commands::write_queue::enqueue_write,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
