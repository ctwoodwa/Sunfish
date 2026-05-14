/**
 * @sunfish/contracts — single entry point for Sunfish ERPNext stack TypeScript types.
 *
 * Namespaces:
 *  - property  : Property, Unit, OccupancyStatus, RentStatus, RentRollRow
 *  - accounting: LedgerEntry, JournalEntry, BankTransaction, PLSummary, PLLineItem, OutstandingInvoice
 *  - tenant    : Tenant, Lease, PaymentRecord, MessageThread
 *  - sync      : SyncStatus, OfflineQueueEntry, ConflictRecord
 *
 * Re-exports from @sunfish/ui-adapters-react contracts surface:
 *  - Integration Atlas types (ADR 0067)
 *  - SystemRequirements types (ADR 0063)
 */

export * from './property.js'
export * from './accounting.js'
export * from './tenant.js'
export * from './sync.js'

// Wayfinder substrate contracts — vendored from ui-adapters-react/src/contracts/.
// Kept in sync with ADR 0067 (Integrations) and ADR 0063-A1.1 (SystemRequirements).
export * from './integrations.js'
export * from './system-requirements.js'
