// W#60 P4 PR 1 — Bridge auth token + keychain status state management.
//
// AuthToken holds the active Bridge auth token; sync code in `sync::pull` /
// `sync::push` reads it at each HTTP call so token rotations take effect
// without restarting the sync task (council R4).
//
// KeychainStatus surfaces the outcome of the OS-keychain master-key derivation
// performed once at setup time. Stored as `Result<(), String>` so the JS side
// can render a graceful banner if the keychain is unavailable (council A1.4),
// instead of the previous design that panicked the Tauri process from inside
// the Stronghold password closure.

use std::sync::Arc;
use tokio::sync::RwLock;

/// Tauri-managed handle for the active Bridge auth token. Initial value comes
/// from the `BRIDGE_TOKEN` env var at startup (dev/CI ergonomics); production
/// sets it via `set_bridge_token` after the frontend loads it from Stronghold.
pub struct AuthToken(pub Arc<RwLock<String>>);

impl AuthToken {
    pub fn new(initial: String) -> Self {
        Self(Arc::new(RwLock::new(initial)))
    }
}

/// Status of the OS-keychain master-key derivation performed at setup time.
/// `Ok` means the key is cached and Stronghold can be opened; `Err` carries
/// the platform error so the frontend can present a precise diagnostic banner.
pub struct KeychainStatus(pub Arc<RwLock<Result<(), String>>>);

impl KeychainStatus {
    pub fn new(result: Result<(), String>) -> Self {
        Self(Arc::new(RwLock::new(result)))
    }
}

/// Tauri-managed handle for the Bridge base URL the app was launched against.
/// Exposed to JS via `get_bridge_url` so the LoginPage can do its A1.3 whoami
/// probe with an absolute URL — relative paths like `/api/v1/whoami` work only
/// in `tauri:dev` mode (Vite proxy) and silently fall through to the SPA asset
/// handler in the bundled build, causing false-positive 200 responses.
pub struct BridgeUrl(pub String);

/// Returns the active Bridge base URL (e.g., `http://localhost:7080`). The JS
/// LoginPage joins this with `/api/v1/whoami` so the Bearer-token probe hits
/// Bridge directly, not the Tauri asset fallback.
#[tauri::command]
pub async fn get_bridge_url(state: tauri::State<'_, BridgeUrl>) -> Result<String, String> {
    Ok(state.0.clone())
}

/// Update the active Bridge auth token. Called by the frontend after reading
/// from Stronghold on boot, or after the user pastes a token in the LoginPage.
/// Empty string clears the token (logout path).
#[tauri::command]
pub async fn set_bridge_token(
    token: String,
    state: tauri::State<'_, AuthToken>,
) -> Result<(), String> {
    let mut t = state.0.write().await;
    *t = token;
    Ok(())
}

/// Returns whether a token is currently set. Used by the frontend on boot to
/// decide whether to seed the state from Stronghold or whether the env-var
/// initialization already provided one.
#[tauri::command]
pub async fn has_bridge_token(state: tauri::State<'_, AuthToken>) -> Result<bool, String> {
    let t = state.0.read().await;
    Ok(!t.is_empty())
}

/// Returns the keychain derivation status. `None` = healthy (master key is
/// cached, Stronghold can be opened). `Some(message)` = the keychain could
/// not be reached at startup and the platform error is surfaced for the JS
/// banner. Council A1.4.
#[tauri::command]
pub async fn keychain_status(
    state: tauri::State<'_, KeychainStatus>,
) -> Result<Option<String>, String> {
    let s = state.0.read().await;
    match &*s {
        Ok(()) => Ok(None),
        Err(e) => Ok(Some(e.clone())),
    }
}
