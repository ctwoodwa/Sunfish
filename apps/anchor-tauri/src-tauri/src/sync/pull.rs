use std::sync::Arc;

use anyhow::Context;
use sqlx::SqlitePool;
use tokio::sync::RwLock;

/// W#60 P4 PR 1 (council R4) — reads the current Bridge auth token from the
/// shared state at each HTTP call. Token rotations (login, logout, re-paste)
/// take effect on the next request without restarting the sync task.
async fn current_token(token: &Arc<RwLock<String>>) -> String {
    token.read().await.clone()
}

pub async fn pull_all(
    pool: &SqlitePool,
    bridge_base_url: &str,
    auth_token: &Arc<RwLock<String>>,
) -> anyhow::Result<()> {
    pull_table(pool, bridge_base_url, auth_token, "properties", "/api/v1/erpnext/properties", "company").await?;
    pull_table(pool, bridge_base_url, auth_token, "leases", "/api/v1/erpnext/leases", "company").await?;
    pull_table(pool, bridge_base_url, auth_token, "maintenance_tickets", "/api/v1/erpnext/maintenance", "company").await?;
    pull_table(pool, bridge_base_url, auth_token, "payments", "/api/v1/erpnext/payments?limit=200", "lease").await?;
    Ok(())
}

async fn pull_table(
    pool: &SqlitePool,
    bridge_base_url: &str,
    auth_token: &Arc<RwLock<String>>,
    table: &str,
    path: &str,
    fk_col: &str,
) -> anyhow::Result<()> {
    let url = format!("{}{}", bridge_base_url, path);
    let token = current_token(auth_token).await;
    let client = reqwest::Client::new();
    let resp = client
        .get(&url)
        .bearer_auth(&token)
        .send()
        .await
        .with_context(|| format!("fetch {url}"))?;

    if !resp.status().is_success() {
        anyhow::bail!("bridge returned {} for {url}", resp.status());
    }

    let items: Vec<serde_json::Value> = resp.json().await.with_context(|| format!("parse {url}"))?;
    let count = items.len() as i64;
    let now = chrono::Utc::now().to_rfc3339();

    for item in &items {
        let name = item["name"].as_str().unwrap_or_default();
        let fk_val = item[fk_col].as_str().unwrap_or_default();
        let data_json = item.to_string();
        sqlx::query(&format!(
            "INSERT OR REPLACE INTO {table} (name, data_json, {fk_col}, synced_at) VALUES (?, ?, ?, ?)"
        ))
        .bind(name)
        .bind(&data_json)
        .bind(fk_val)
        .bind(&now)
        .execute(pool)
        .await
        .with_context(|| format!("upsert into {table}"))?;
    }

    sqlx::query(
        "INSERT OR REPLACE INTO sync_metadata (table_name, last_synced, record_count) VALUES (?, ?, ?)",
    )
    .bind(table)
    .bind(&now)
    .bind(count)
    .execute(pool)
    .await
    .with_context(|| "update sync_metadata")?;

    Ok(())
}
