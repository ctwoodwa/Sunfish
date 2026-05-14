pub mod commands;
pub mod db;
pub mod sync;

use tauri::Manager;

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(
            tauri_plugin_stronghold::Builder::new(|password| {
                // Phase 3: stronghold key derivation from device identifier
                // Phase 4: integrate OS keychain here
                password.as_bytes().to_vec()
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
