// W#60 P4 PR 1 — Bridge auth token state management.
//
// Stores the active Bridge auth token in Tauri-managed state so the JS side can
// load it from Stronghold on boot and update it after login/logout. The actual
// sync code (`sync::pull`, `sync::push`) currently consumes a token captured at
// startup from the `BRIDGE_TOKEN` env var — wiring that to read from this state
// is a follow-up scope-cut from PR 1 (see PR description); for now this command
// is the JS-facing surface that lets the frontend land the Stronghold flow.

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
