pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_sql::Builder::default().build())
        .plugin(
            tauri_plugin_stronghold::Builder::new(|password| {
                // Phase 3: stronghold key derivation from device identifier
                // Phase 4: integrate OS keychain here
                password.as_bytes().to_vec()
            })
            .build(),
        )
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
