// Always use the Windows GUI subsystem (no console window) — including in
// debug builds. The previous `cfg_attr(not(debug_assertions), …)` only
// suppressed the console in release, so anyone running a debug MSI got a
// stray cmd.exe-style window alongside Anchor. We send diagnostics through
// `eprintln!` → captured by the Windows event log / tracing, not stdout,
// so losing the debug console doesn't cost us logging.
#![windows_subsystem = "windows"]

fn main() {
    anchor_tauri_lib::run();
}
