use sqlx::SqlitePool;
use tauri::State;

#[tauri::command]
pub async fn get_cached_properties(pool: State<'_, SqlitePool>) -> Result<Vec<serde_json::Value>, String> {
    let rows = sqlx::query_scalar::<_, String>("SELECT data_json FROM properties")
        .fetch_all(pool.inner())
        .await
        .map_err(|e| e.to_string())?;
    rows.iter()
        .map(|r| serde_json::from_str(r).map_err(|e| e.to_string()))
        .collect()
}

#[tauri::command]
pub async fn get_cached_leases(pool: State<'_, SqlitePool>) -> Result<Vec<serde_json::Value>, String> {
    let rows = sqlx::query_scalar::<_, String>("SELECT data_json FROM leases")
        .fetch_all(pool.inner())
        .await
        .map_err(|e| e.to_string())?;
    rows.iter()
        .map(|r| serde_json::from_str(r).map_err(|e| e.to_string()))
        .collect()
}

#[tauri::command]
pub async fn get_cached_payments(pool: State<'_, SqlitePool>) -> Result<Vec<serde_json::Value>, String> {
    let rows = sqlx::query_scalar::<_, String>("SELECT data_json FROM payments")
        .fetch_all(pool.inner())
        .await
        .map_err(|e| e.to_string())?;
    rows.iter()
        .map(|r| serde_json::from_str(r).map_err(|e| e.to_string()))
        .collect()
}

#[tauri::command]
pub async fn get_cached_maintenance_tickets(
    pool: State<'_, SqlitePool>,
) -> Result<Vec<serde_json::Value>, String> {
    let rows = sqlx::query_scalar::<_, String>("SELECT data_json FROM maintenance_tickets")
        .fetch_all(pool.inner())
        .await
        .map_err(|e| e.to_string())?;
    rows.iter()
        .map(|r| serde_json::from_str(r).map_err(|e| e.to_string()))
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use sqlx::SqlitePool;

    async fn test_pool() -> SqlitePool {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::query(crate::db::SCHEMA).execute(&pool).await.unwrap();
        pool
    }

    #[tokio::test]
    async fn get_cached_properties_returns_empty_for_fresh_db() {
        let pool = test_pool().await;
        let result: Vec<serde_json::Value> =
            sqlx::query_scalar::<_, String>("SELECT data_json FROM properties")
                .fetch_all(&pool)
                .await
                .unwrap()
                .iter()
                .map(|r| serde_json::from_str(r).unwrap())
                .collect();
        assert!(result.is_empty());
    }

    #[tokio::test]
    async fn get_cached_properties_returns_inserted_row() {
        let pool = test_pool().await;
        let data = serde_json::json!({"name": "PROP-0001", "property_name": "150 Lexington Ct"});
        sqlx::query(
            "INSERT INTO properties (name, data_json, company, synced_at) VALUES (?, ?, ?, ?)",
        )
        .bind("PROP-0001")
        .bind(data.to_string())
        .bind("test-company")
        .bind("2026-05-14T00:00:00Z")
        .execute(&pool)
        .await
        .unwrap();

        let rows: Vec<serde_json::Value> =
            sqlx::query_scalar::<_, String>("SELECT data_json FROM properties")
                .fetch_all(&pool)
                .await
                .unwrap()
                .iter()
                .map(|r| serde_json::from_str(r).unwrap())
                .collect();
        assert_eq!(rows.len(), 1);
        assert_eq!(rows[0]["name"], "PROP-0001");
    }
}
