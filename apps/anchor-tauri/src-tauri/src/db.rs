use sqlx::SqlitePool;
use std::path::Path;

pub const SCHEMA: &str = r#"
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
    table_name   TEXT PRIMARY KEY,
    last_synced  TEXT NOT NULL,
    record_count INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS write_queue (
    id           TEXT PRIMARY KEY,
    doctype      TEXT NOT NULL,
    op_type      TEXT NOT NULL,
    doc_name     TEXT,
    payload_json TEXT NOT NULL,
    created_at   TEXT NOT NULL,
    synced_at    TEXT,
    error        TEXT
);
"#;

pub async fn open(data_dir: &Path) -> anyhow::Result<SqlitePool> {
    std::fs::create_dir_all(data_dir)?;
    let db_path = data_dir.join("anchor.db");
    let url = format!("sqlite://{}?mode=rwc", db_path.display());
    let pool = SqlitePool::connect(&url).await?;
    sqlx::query(SCHEMA).execute(&pool).await?;
    Ok(pool)
}
