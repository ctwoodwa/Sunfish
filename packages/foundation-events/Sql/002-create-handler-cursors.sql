-- foundation-events migration 002 — create event_handler_cursors +
-- event_handler_failures.
-- Per cross-cluster-event-bus-design.md §5 (per-replica position
-- cursors) + §6 (failure + retry backoff schedule).
--
-- event_handler_cursors is MUTABLE — cursor-advance is the only
-- write. NOT cross-replica synced; each replica drives its own
-- dispatcher independently.

CREATE TABLE IF NOT EXISTS event_handler_cursors (
    handler_id              TEXT NOT NULL,
    tenant_id               TEXT NOT NULL,
    last_handled_event_id   TEXT,
    last_handled_at         TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (handler_id, tenant_id)
);

CREATE TABLE IF NOT EXISTS event_handler_failures (
    id              TEXT NOT NULL PRIMARY KEY,
    handler_id      TEXT NOT NULL,
    event_id        TEXT NOT NULL,
    tenant_id       TEXT NOT NULL,
    attempt_number  INTEGER NOT NULL,
    failed_at       TEXT NOT NULL,
    error_message   TEXT NOT NULL,
    next_retry_at   TEXT,
    resolved_at     TEXT
);

CREATE INDEX IF NOT EXISTS idx_handler_failures_retry
    ON event_handler_failures(next_retry_at)
    WHERE resolved_at IS NULL AND next_retry_at IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_handler_failures_event
    ON event_handler_failures(handler_id, event_id);
