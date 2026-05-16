use std::sync::Arc;

use anyhow::Context;
use sqlx::{Row, SqlitePool};
use tokio::sync::RwLock;

pub async fn drain_write_queue(
    pool: &SqlitePool,
    bridge_base_url: &str,
    auth_token: &Arc<RwLock<String>>,
) -> anyhow::Result<()> {
    let pending = sqlx::query(
        "SELECT id, doctype, op_type, doc_name, payload_json FROM write_queue
         WHERE synced_at IS NULL AND error IS NULL
         ORDER BY created_at ASC",
    )
    .fetch_all(pool)
    .await
    .context("fetch pending write_queue")?;

    for row in pending {
        let id: String = row.get("id");
        let result = sync_one_entry(&row, bridge_base_url, auth_token).await;
        match result {
            Ok(_) => {
                sqlx::query(
                    "UPDATE write_queue SET synced_at = datetime('now') WHERE id = ?",
                )
                .bind(&id)
                .execute(pool)
                .await
                .with_context(|| format!("mark synced {id}"))?;
            }
            Err(e) => {
                eprintln!("[push] sync_one_entry {id} failed: {e}");
                sqlx::query("UPDATE write_queue SET error = ? WHERE id = ?")
                    .bind(e.to_string())
                    .bind(&id)
                    .execute(pool)
                    .await
                    .with_context(|| format!("record error for {id}"))?;
            }
        }
    }
    Ok(())
}

async fn sync_one_entry(
    row: &sqlx::sqlite::SqliteRow,
    bridge_base_url: &str,
    auth_token: &Arc<RwLock<String>>,
) -> anyhow::Result<()> {
    // W#60 P4 PR 1 (council R4) — read the token fresh per request so
    // rotation takes effect without restarting the sync task.
    let token = auth_token.read().await.clone();
    let doctype: &str = row.get("doctype");
    let op_type: &str = row.get("op_type");
    let doc_name: Option<&str> = row.try_get("doc_name").ok().flatten();
    let payload_json: &str = row.get("payload_json");

    let body: serde_json::Value =
        serde_json::from_str(payload_json).context("parse payload_json")?;

    let client = reqwest::Client::new();

    let url = match (doctype, op_type) {
        ("Maintenance Note", "create") => {
            format!("{bridge_base_url}/api/v1/erpnext/maintenance")
        }
        ("Maintenance Note", "update") => {
            let name = doc_name.context("doc_name required for update")?;
            if name.is_empty()
                || name.len() > 140
                || !name.chars().all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
            {
                anyhow::bail!("invalid doc_name: {name}");
            }
            format!("{bridge_base_url}/api/v1/erpnext/maintenance/{name}")
        }
        _ => anyhow::bail!("unknown doctype/op_type: {doctype}/{op_type}"),
    };

    let req = match op_type {
        "create" => client.post(&url),
        "update" => client.patch(&url),
        _ => anyhow::bail!("unknown op_type: {op_type}"),
    };

    let resp = req
        .bearer_auth(&token)
        .json(&body)
        .send()
        .await
        .with_context(|| format!("send to {url}"))?;

    if !resp.status().is_success() {
        anyhow::bail!("bridge returned {} for {url}", resp.status());
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    async fn test_pool() -> SqlitePool {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::query(crate::db::SCHEMA).execute(&pool).await.unwrap();
        pool
    }

    #[tokio::test]
    async fn drain_empty_queue_is_noop() {
        let pool = test_pool().await;
        let result = drain_write_queue(&pool, "http://localhost:7080", &Arc::new(RwLock::new(String::new()))).await;
        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn drain_skips_already_synced_rows() {
        let pool = test_pool().await;
        sqlx::query(
            "INSERT INTO write_queue
             (id, doctype, op_type, payload_json, created_at, synced_at)
             VALUES (?, ?, ?, ?, datetime('now'), datetime('now'))",
        )
        .bind("synced-1")
        .bind("Maintenance Note")
        .bind("create")
        .bind(r#"{"ticket":"MT-001","content":"test"}"#)
        .execute(&pool)
        .await
        .unwrap();

        // drain with invalid URL — if the already-synced row were picked up, this would fail
        let result = drain_write_queue(&pool, "http://invalid-host-xyz:9999", &Arc::new(RwLock::new(String::new()))).await;
        assert!(result.is_ok(), "drain should succeed when no pending rows");
    }

    #[tokio::test]
    async fn drain_marks_error_on_network_failure() {
        let pool = test_pool().await;
        sqlx::query(
            "INSERT INTO write_queue
             (id, doctype, op_type, payload_json, created_at)
             VALUES (?, ?, ?, ?, datetime('now'))",
        )
        .bind("pending-1")
        .bind("Maintenance Note")
        .bind("create")
        .bind(r#"{"ticket":"MT-001","content":"test"}"#)
        .execute(&pool)
        .await
        .unwrap();

        // drain with invalid URL — row should get error recorded, not panic
        let result = drain_write_queue(&pool, "http://invalid-host-xyz:9999", &Arc::new(RwLock::new(String::new()))).await;
        assert!(result.is_ok(), "drain should not propagate per-row errors");

        let error: Option<String> =
            sqlx::query_scalar("SELECT error FROM write_queue WHERE id = ?")
                .bind("pending-1")
                .fetch_one(&pool)
                .await
                .unwrap();
        assert!(error.is_some(), "row should have error recorded");
        assert!(error.unwrap().len() > 0);
    }
}
