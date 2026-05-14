use sqlx::SqlitePool;
use tauri::State;
use uuid::Uuid;

fn validate_doc_name(name: &str) -> Result<(), String> {
    if name.is_empty()
        || name.len() > 140
        || !name.chars().all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
    {
        return Err(format!("invalid doc_name: {name}"));
    }
    Ok(())
}

#[tauri::command]
pub async fn enqueue_write(
    pool: State<'_, SqlitePool>,
    doctype: String,
    op_type: String,
    doc_name: Option<String>,
    payload_json: String,
) -> Result<String, String> {
    if let Some(ref name) = doc_name {
        validate_doc_name(name)?;
    }
    let id = Uuid::new_v4().to_string();
    sqlx::query(
        "INSERT INTO write_queue (id, doctype, op_type, doc_name, payload_json, created_at)
         VALUES (?, ?, ?, ?, ?, datetime('now'))",
    )
    .bind(&id)
    .bind(&doctype)
    .bind(&op_type)
    .bind(&doc_name)
    .bind(&payload_json)
    .execute(pool.inner())
    .await
    .map_err(|e| e.to_string())?;
    Ok(id)
}

#[cfg(test)]
mod tests {
    use sqlx::SqlitePool;

    async fn test_pool() -> SqlitePool {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::query(crate::db::SCHEMA).execute(&pool).await.unwrap();
        pool
    }

    async fn insert_write_queue(pool: &SqlitePool, doctype: &str, payload: &str) -> String {
        let id = uuid::Uuid::new_v4().to_string();
        sqlx::query(
            "INSERT INTO write_queue (id, doctype, op_type, payload_json, created_at)
             VALUES (?, ?, ?, ?, datetime('now'))",
        )
        .bind(&id)
        .bind(doctype)
        .bind("create")
        .bind(payload)
        .execute(pool)
        .await
        .unwrap();
        id
    }

    #[tokio::test]
    async fn write_queue_row_inserts_with_uuid() {
        let pool = test_pool().await;
        let id = insert_write_queue(&pool, "Maintenance Note", r#"{"ticket":"MT-001"}"#).await;
        assert!(!id.is_empty());
        let count: i64 = sqlx::query_scalar("SELECT COUNT(*) FROM write_queue WHERE id = ?")
            .bind(&id)
            .fetch_one(&pool)
            .await
            .unwrap();
        assert_eq!(count, 1);
    }

    #[tokio::test]
    async fn write_queue_row_stores_correct_payload() {
        let pool = test_pool().await;
        let payload = r#"{"ticket":"MT-002","content":"window broken"}"#;
        let id = insert_write_queue(&pool, "Maintenance Note", payload).await;

        let stored: String =
            sqlx::query_scalar("SELECT payload_json FROM write_queue WHERE id = ?")
                .bind(&id)
                .fetch_one(&pool)
                .await
                .unwrap();
        assert_eq!(stored, payload);
    }

    #[tokio::test]
    async fn write_queue_row_starts_unsynced() {
        let pool = test_pool().await;
        let id = insert_write_queue(&pool, "Maintenance Note", r#"{}"#).await;

        let synced_at: Option<String> =
            sqlx::query_scalar("SELECT synced_at FROM write_queue WHERE id = ?")
                .bind(&id)
                .fetch_one(&pool)
                .await
                .unwrap();
        assert!(synced_at.is_none(), "new row should not be synced");
    }
}
