-- foundation-events migration 001 — create domain_events table.
-- Append-only per crdt-friendly-schema-conventions.md §4.
-- Per cross-cluster-event-bus-design.md §1 storage shape.

CREATE TABLE IF NOT EXISTS domain_events (
    event_id                TEXT NOT NULL PRIMARY KEY,        -- ULID
    event_type              TEXT NOT NULL,                    -- "Financial.JournalEntryPosted"
    schema_version          INTEGER NOT NULL,                 -- bumped on payload-shape changes
    occurred_at             TEXT NOT NULL,                    -- ISO-8601 UTC
    recorded_at_utc         TEXT NOT NULL,                    -- ISO-8601 UTC; store-side write timestamp
    tenant_id               TEXT NOT NULL,
    originating_replica_id  TEXT NOT NULL,                    -- ReplicaId value
    idempotency_key         TEXT NOT NULL,
    causation_id            TEXT,
    correlation_id          TEXT,
    producer_cluster        TEXT NOT NULL,                    -- derived from event_type prefix
    payload_json            TEXT NOT NULL,                    -- serialized DomainEventEnvelope.Payload
    created_at              TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_domain_events_idempotency
    ON domain_events(tenant_id, idempotency_key);

CREATE INDEX IF NOT EXISTS idx_domain_events_type_recorded
    ON domain_events(tenant_id, event_type, recorded_at_utc);

CREATE INDEX IF NOT EXISTS idx_domain_events_correlation
    ON domain_events(tenant_id, correlation_id)
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_domain_events_tenant_eventid
    ON domain_events(tenant_id, event_id);
    -- For cursor-after-eventid queries from IEventReader.
