# ERPNext → Anchor Migration Importer — Specification

**Status:** Draft v1 (XO authored 2026-05-16)
**Owner:** XO (research session)
**License posture:** Sunfish output MIT (per ADR 0088 §2)
**Pipeline:** `sunfish-feature-change` — Stage 03 (package design) level specification
**Stage 02 parents:**
[`icm/02_architecture/blocks-financial-schema-design.md`](../../icm/02_architecture/blocks-financial-schema-design.md) §10,
[`icm/02_architecture/blocks-people-schema-design.md`](../../icm/02_architecture/blocks-people-schema-design.md) §3.1,
[`icm/02_architecture/blocks-reports-schema-design.md`](../../icm/02_architecture/blocks-reports-schema-design.md) §8
**ADR refs:** [ADR 0088 Path II](../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md);
naming-ratification ruling
`coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`

---

## 0. Document purpose

This document specifies the one-way migration importer that reads exported
ERPNext data and writes Anchor-native domain records under the `blocks-financial-*`,
`blocks-people-*`, `blocks-property-*`, and `blocks-reports-*` clusters.

It expands the high-level §10 of `blocks-financial-schema-design.md` to a
full Stage 03 / package-design specification suitable for handing to an
implementer (`cob`). Scope, source format, mapping tables, importer
algorithm, idempotency, error handling, acceptance criteria, CLI surface,
clean-room discipline, and open questions are all here.

The importer is **per ADR 0088 Path II** — Anchor is the all-in-one
local-first runtime; ERPNext is the legacy engine being retired. The
importer enables the cutover from the W#60 P1–P3 ERPNext detour to the
native domain.

---

## 1. Scope

### 1.1 What this importer does

- **One-way ingestion** of ERPNext data into Anchor's native domain model.
- **Source of truth during migration:** the ERPNext export file (JSON / CSV /
  Excel as produced by `bench export-fixtures` and `frappe.client.export_to_excel`).
- **Idempotent re-runs:** running the importer against the same export
  directory a second time produces no new records — only updates to records
  whose ERPNext-side `modified` timestamp moved forward.
- **Preserves $7.6M Wave-history-migrated accounting** plus 4-LLC operational
  data (Acero Properties LLC, Bosco Properties LLC, Escola Properties LLC,
  Shirin Properties LLC — all VA-located).
- **Audited:** every record import emits a `MigrationAuditLog` row capturing
  source `name`, target `id`, action (insert/update/skip), and pass number.

### 1.2 What this importer does NOT do

- **No write-back to ERPNext.** This is strictly read-only against the
  source export.
- **No live connection to a running Frappe / ERPNext instance.** The
  importer consumes a static export directory; the user is responsible
  for producing the export from ERPNext (via documented `bench` commands).
- **No code reuse from ERPNext or Frappe.** Per ADR 0088 §3 (clean-room
  implementation discipline), the importer reads ERPNext's export
  **data format** (a public schema, treated as a data interchange
  contract) — not its controllers, validators, workflow code, or
  DocType definitions.
- **No item/SKU catalog import.** AR/AP lines de-itemize into
  description + amount (per `blocks-financial-schema-design.md` §10.5).
- **No stock/inventory accounting.** Out of scope for Phase 1.
- **No HR/employee data import.** Belongs to Phase 3 (`blocks-people-*`)
  — explicitly excluded from the Phase 1 importer.
- **No ERPNext print formats, custom scripts, custom DocTypes, workflows,
  or reports.** These are presentation/control-flow artifacts; the
  importer transports data only.

### 1.3 What the importer guarantees

- **Transactional commit boundaries per pass** (see §5). A failed
  pass rolls back its own writes and surfaces a summary; prior passes
  are retained. The user can fix the offending input and resume from
  the failed pass.
- **Trial-balance balance.** Post-import, `Σ debits == Σ credits` across
  every `JournalEntry` per chart (this is enforced by the same
  `postJournalEntry` algorithm — `blocks-financial-schema-design.md`
  §6.1 — that runs for non-migration entries).
- **Reconciliation report.** Pass 6 produces a per-chart reconciliation
  document (trial balance vs ERPNext snapshot; AR aging diff; AP aging
  diff; per-LLC P&L diff). Diffs over a configurable threshold (default
  $0.01) halt the import.
- **External reference preservation.** Every imported record carries
  `externalRef = { source: "erpnext", id: <docname>, version: <modified> }`
  for round-trip trace and idempotent re-import.

---

## 2. Source format — ERPNext export shape

### 2.1 Export-production command (out-of-importer; documented for cutover)

The CO produces the export from the existing Mac-ERPNext instance via:

```bash
# From inside the ERPNext bench (one-time, on the source machine):
bench --site <site> export-fixtures
# Produces a directory of JSON files per DocType under
# <bench-root>/apps/<app>/<app>/fixtures/

# Plus per-DocType data dumps (CO-prepared one-time per migration):
bench --site <site> execute frappe.utils.exporter.export_data \
  --doctype "Account" --output /tmp/erpnext-export/account.json
# (Repeated per relevant DocType; see §3 list below.)
```

The importer does **not** execute these commands. It consumes the output
directory as a static input.

### 2.2 Expected export-directory layout

```
<export-root>/
├── manifest.json                  # importer-side; written by CO's export script
│                                  # { erpnextVersion, exportedAt, siteName,
│                                  #   companies: ["Acero", "Bosco", ...] }
├── Company.json                   # all companies (one chart per company)
├── Account.json                   # full chart-of-accounts tree
├── FiscalYear.json
├── Customer.json
├── Supplier.json
├── Address.json                   # addresses linked to Customer/Supplier
├── Contact.json                   # contact records
├── TaxCategory.json
├── SalesTaxesAndChargesTemplate.json
├── PurchaseTaxesAndChargesTemplate.json
├── JournalEntry.json              # header records
├── JournalEntryAccount.json       # child-table rows (joined by parent name)
├── SalesInvoice.json
├── SalesInvoiceItem.json          # child-table rows
├── PurchaseInvoice.json
├── PurchaseInvoiceItem.json
├── PaymentEntry.json
├── PaymentEntryReference.json     # child-table rows linking PE to invoices/bills
├── Budget.json                    # optional (Phase 1 may skip)
├── BudgetAccount.json             # child-table rows
├── CostCenter.json
├── Property.json                  # custom DocType if Mac-ERPNext defined one;
│                                  # OPTIONAL — may not exist
├── Lease.json                     # custom DocType if defined; OPTIONAL
└── _unmapped/                     # any DocType files the CO chose to export
                                   # but the importer does not understand;
                                   # logged + skipped, not an error
```

### 2.3 Per-file JSON structure (ERPNext convention)

Each `<DocType>.json` is a **single JSON array** of objects. Each object is
one ERPNext document. Common fields appear on every DocType:

```json
{
  "name": "ACC-ACC-2024-00001",      // stable identifier (DocType-scoped unique)
  "doctype": "Account",
  "owner": "user@example.com",       // ignored by importer
  "creation": "2024-01-15 09:34:22", // RFC 3339-ish; used for ordering
  "modified": "2024-08-02 11:02:18", // version key for idempotency
  "modified_by": "user@example.com", // ignored
  "docstatus": 0,                    // 0=Draft, 1=Submitted, 2=Cancelled
  // ...DocType-specific fields...
}
```

Child-table rows (e.g. `JournalEntryAccount`) carry parent linkage:

```json
{
  "name": "JEACC-0000123",
  "doctype": "Journal Entry Account",
  "parent": "ACC-JV-2024-00045",   // parent JournalEntry.name
  "parenttype": "Journal Entry",
  "parentfield": "accounts",
  "idx": 1,                          // 1-based row index within parent
  // ...child-specific fields...
}
```

The importer joins child rows to parents in-memory by `parent` field at
load time. Parent ordering is preserved by sorting on `idx` ascending.

### 2.4 Encoding + character handling

- All JSON is UTF-8.
- Date fields are strings in `YYYY-MM-DD` (`Date`) or
  `YYYY-MM-DD HH:MM:SS` (`Datetime`). The importer converts to ISO 8601
  on ingest.
- Monetary fields are JSON numbers (ERPNext default precision is 2 dp).
  The importer multiplies by 100 to convert to integer-minor-units
  (USD cents) per the canonical money representation
  (`blocks-financial-schema-design.md` §7).
- Null values are JSON `null` or missing keys; the importer treats both
  equivalently.

### 2.5 Source-shape assumptions (documented for future-proofing)

The importer is calibrated to the ERPNext **v14 / v15 export shape** (the
version family running on CO's Mac instance). Field names referenced
throughout §3 (`account_name`, `parent_account`, `voucher_type`,
`is_opening`, etc.) match v14. If a future export uses a different ERPNext
version with renamed fields, the importer halts at the manifest-validation
step (see §6.1) and surfaces a `ManifestVersionMismatch` error naming the
unknown fields.

---

## 3. DocType-to-Anchor mapping table

The full mapping table; each row is "what ERPNext field maps to what
Anchor field, in what target entity."

### 3.1 Master table

| ERPNext DocType | Target cluster | Target entity | Notes |
|---|---|---|---|
| `Company` | `blocks-financial-*` (foundation) | `LegalEntity` + `ChartOfAccounts` | One chart per company. CO portfolio = 4 charts. |
| `FiscalYear` | `blocks-financial-periods` | `FiscalYear` + synthesized `FiscalPeriod[]` | Monthly periods derived from FY start/end + chart's `fiscalYearStart`. |
| `Account` | `blocks-financial-chart` | `GLAccount` (the existing C# entity per ratified naming) | Tree topological sort; see §4 pass 2. |
| `Cost Center` | `blocks-financial-chart` | `Classification` | OR map to `Property.id` if the cost-center name resolves to a known property (heuristic; see §3.4). |
| `Tax Category` | `blocks-financial-tax` | `TaxJurisdiction` | Best-effort; may produce stubs the user manually re-jurisdictions post-import. |
| `Sales Taxes and Charges Template` | `blocks-financial-tax` | `TaxCode` + `TaxRate[]` | Composite: one TaxCode per template; one TaxRate per row in the template's `taxes` child table. |
| `Purchase Taxes and Charges Template` | `blocks-financial-tax` | `TaxCode` + `TaxRate[]` | Same shape as Sales. |
| `Address` | `blocks-people-*` | `PartyAddress` (sub-entity) | Resolved by `links` child table to the owning Customer/Supplier party. |
| `Contact` | `blocks-people-*` | `Party` (kind=person) + `EmailAddress[]` + `PhoneNumber[]` | If a contact has no commercial role, becomes a bare Party. |
| `Customer` | `blocks-people-*` | `Party` (kind=org\|person) + `PartyRole` (role=customer) + `Customer` extension | Customer extension carries `arAccountId`, `customerNumber`. |
| `Supplier` | `blocks-people-*` | `Party` + `PartyRole` (role=vendor) + `Vendor` extension | Vendor extension (defined in `blocks-people-*` §3.5) carries `apAccountId`, `vendorNumber`, `is1099Eligible`. |
| `Journal Entry` | `blocks-financial-ledger` | `JournalEntry` | Header. |
| `Journal Entry Account` | `blocks-financial-ledger` | `JournalEntryLine` | Child rows; one line per JE-account row. |
| `GL Entry` | (not imported) | — | Re-derived from `JournalEntry` post-import. Informational only. |
| `Sales Invoice` | `blocks-financial-ar` | `Invoice` | Header. |
| `Sales Invoice Item` | `blocks-financial-ar` | `InvoiceLine` | De-itemized (Item-link discarded; description + amount preserved). |
| `Purchase Invoice` | `blocks-financial-ap` | `Bill` | Header. |
| `Purchase Invoice Item` | `blocks-financial-ap` | `BillLine` | De-itemized. |
| `Payment Entry` | `blocks-financial-payments` | `Payment` | Header. |
| `Payment Entry Reference` | `blocks-financial-payments` | `PaymentApplication` | One per `references` child row. |
| `Item` | (not imported) | — | AR/AP lines de-itemized; physical inventory N/A for property. |
| `Stock Entry` | (not imported) | — | Out of scope; no inventory accounting in Phase 1. |
| `Budget` | `blocks-financial-budget` (Phase 3) | `Budget` | Optional; can defer to Phase 3 import without affecting Phase 1 acceptance. |
| `Budget Account` | `blocks-financial-budget` (Phase 3) | `BudgetLine` + `BudgetPeriod` | Optional. |
| `Property` (custom DocType) | `blocks-property-*` | `Property` | OPTIONAL — only if CO's ERPNext instance has a custom Property DocType. If absent, properties are seeded from `Cost Center` heuristic OR manually entered post-import. |
| `Lease` (custom DocType) | `blocks-property-*` | `Lease` | OPTIONAL — same disposition as Property. |
| `Employee` / `Salary Slip` / `Leave Application` | (not imported) | — | Phase 3 (`blocks-people-*` HR sub-cluster); explicitly excluded from this Phase 1 importer. |
| `Letter Head` / `Print Format` / `Custom Field` | (not imported) | — | Presentation/control-flow; out of scope. |

### 3.2 `account_type` enum mapping (Account DocType)

ERPNext's `account_type` field is the most-information-dense field on
`Account`; it drives both `GLAccountType` (top-level) and Anchor's
`AccountSubtype`. The full mapping table:

| ERPNext `account_type` | Anchor `GLAccountType` (existing enum) | Anchor `AccountSubtype` (new, per `blocks-financial-schema-design.md` §3.1) |
|---|---|---|
| `Bank` | `Asset` | `BankAccount` |
| `Cash` | `Asset` | `BankAccount` |
| `Receivable` | `Asset` | `AccountsReceivable` |
| `Stock` | `Asset` | `InventoryAsset` |
| `Fixed Asset` | `Asset` | `FixedAsset` |
| `Accumulated Depreciation` | `Asset` | `AccumulatedDepreciation` |
| `Current Asset` | `Asset` | `CurrentAsset` |
| (empty + group ancestor is Asset) | `Asset` | `OtherAsset` |
| `Payable` | `Liability` | `AccountsPayable` |
| `Tax` | `Liability` | `TaxesPayable` |
| `Current Liability` | `Liability` | `CurrentLiability` |
| (empty + group ancestor is Liability) | `Liability` | `OtherLiability` |
| `Equity` | `Equity` | `OwnersEquity` |
| (empty + group ancestor is Equity) | `Equity` | `OwnersEquity` |
| `Income Account` | `Revenue` | `OperatingIncome` |
| (empty + group ancestor is Income) | `Revenue` | `OtherIncome` |
| `Cost of Goods Sold` | `Expense` | `CostOfGoodsSold` |
| `Expense Account` | `Expense` | `OperatingExpense` |
| `Depreciation` | `Expense` | `DepreciationExpense` |
| `Round Off` | `Expense` | `OtherExpense` |
| (empty + group ancestor is Expense) | `Expense` | `OperatingExpense` |
| `Temporary` | (skipped — internal Frappe scratch) | — |
| (unmappable) | — | halts pass 2 with `UnknownAccountType { erpnextValue, account.name }` |

**Algorithm for empty/ambiguous `account_type`:** walk up `parent_account`
until a populated `account_type` (or a group node whose name unambiguously
implies a top-level category, e.g. "Income", "Expense", "Application of
Funds") is found.

### 3.3 `voucher_type` enum mapping (Journal Entry DocType)

| ERPNext `voucher_type` | Anchor `JournalEntrySource` |
|---|---|
| `Journal Entry` | `Manual` |
| `Opening Entry` | `Migration` |
| `Bank Entry` | `Payment` |
| `Cash Entry` | `Payment` |
| `Credit Card Entry` | `Payment` |
| `Contra Entry` | `Manual` |
| `Debit Note` | `Reversal` (in AP context) |
| `Credit Note` | `Reversal` (in AR context) |
| `Depreciation Entry` | `Depreciation` |
| `Excise Entry` | `Manual` |
| `Write Off Entry` | `Adjusting` |
| `Stock Entry` | (skipped — inventory; out of scope) |
| `Inter Company Journal Entry` | `Manual` (marked with note) |
| `Closing Entry` | `Closing` |
| (unknown) | `Manual` + warning logged |

### 3.4 Cost-Center → Property heuristic

ERPNext often abuses `Cost Center` as a property identifier when the
Frappe property module isn't installed. The importer heuristic:

1. Load `Cost Center` records for the company.
2. For each cost-center, attempt to resolve to a known `Property.id`:
   - If a custom `Property` DocType was exported AND the cost-center
     `cost_center_name` matches a property's `name` / `address_short` /
     `nickname` — resolve to that Property.
   - If no custom DocType but the cost-center name matches a known
     pattern (e.g. street-address prefix from a CO-provided
     `property-aliases.json` file in the export root) — resolve via
     alias map.
   - Otherwise — create a `Classification` record (a free-form
     dimensional tag) with the cost-center name preserved.
3. Each resolved property/classification gets an `externalRef` pointing
   back to the cost-center `name` for trace.

The alias-map approach lets the CO author a one-time mapping file before
import without requiring code changes. Default file path:
`<export-root>/property-aliases.json`:

```json
[
  { "costCenterName": "Acero - 123 Main St", "propertyId": "PROP-0001" },
  { "costCenterName": "Bosco - 456 Oak Ave",  "propertyId": "PROP-0002" }
]
```

### 3.5 Currency handling

Anchor v1 is single-currency per chart (`ChartOfAccounts.baseCurrency`).
The importer:

1. Reads `Company.default_currency` from ERPNext.
2. Sets the target chart's `baseCurrency` to that value.
3. Validates every transactional record (`debit_in_account_currency`,
   `credit_in_account_currency`, `Sales Invoice.currency`, etc.)
   matches the chart's `baseCurrency`. Mismatches go to the reject-bin
   (§7) — not an automatic halt, but a per-record skip with reason
   `MultiCurrencyNotSupportedInV1`.
4. For the 4-LLC CO portfolio, all are USD; this branch is a guardrail,
   not the common path.

### 3.6 `docstatus` handling

ERPNext's `docstatus`:
- `0` = Draft → maps to Anchor `Draft` status on Invoice/Bill/JE; skipped
  for Payments (draft payments don't exist in Anchor v1 — they're handled
  via the `Draft` status on the source invoice/bill instead).
- `1` = Submitted → maps to Anchor `Issued` / `Received` / `Posted` /
  `Cleared` (status varies per target entity).
- `2` = Cancelled → maps to Anchor `Voided` (Invoice/Bill) /
  `Reversed` (JournalEntry) / `Voided` (Payment).

Cancelled records that lack a corresponding reversing entry in ERPNext
go to the reject-bin with `CancelledWithoutReversal { name }` — the
importer cannot synthesize a reversal without knowing which line to
reverse.

---

## 4. Six-pass importer algorithm

The importer runs six passes in strict sequence per company/chart.
Cross-company passes are independent; the user can choose to import one
LLC at a time.

### 4.1 Pass 1 — Chart of accounts

**Purpose:** Land the account hierarchy. Subsequent passes depend on
every `accountId` in Pass 2+ resolving.

**Steps:**

1. Load `Company.json`. For each company in `manifest.json.companies`,
   create-or-update a `LegalEntity` + `ChartOfAccounts` pair.
2. Load `Account.json`. Filter to the target company.
3. **Topological sort** by `parent_account`. ERPNext stores parent as a
   string name reference; the sort traverses parent-first so foreign-key
   targets exist before children.
4. For each account in sorted order:
   - Compute `GLAccountType` + `AccountSubtype` per the §3.2 mapping.
     Walk up the tree if `account_type` is empty.
   - Resolve `parent_account` to the previously-imported `GLAccountId`.
   - Insert-or-update via `(externalRef.source, externalRef.id)` key.
   - Set `isPostable = !is_group`.
   - Set `isActive = !disabled`.
   - Persist.
5. Run Pass 1 validation:
   - Every `parentAccountId`, if non-null, resolves to a present account.
   - Every account's `chartId` matches the target chart.
   - Every leaf (`isPostable=true`) account has a populated `code` and
     `name`.

**Commit boundary:** Pass 1 is a single SQLite transaction. Either all
accounts land or none do.

**Failure modes:**
- Cyclic `parent_account` chain → halt with `CyclicAccountTree { cycle: [...] }`.
- Unknown `account_type` after parent-walk → halt with
  `UnknownAccountType { erpnextValue, accountName }`.

### 4.2 Pass 2 — Reference data (parties + tax + periods)

**Purpose:** Land non-transactional master data that subsequent
transactional passes reference.

**Sub-passes (in order; each in its own transaction):**

1. **FiscalYears + FiscalPeriods.**
   - Load `FiscalYear.json`. For each year overlapping the company's
     existence, create a `FiscalYear` record per
     `blocks-financial-schema-design.md` §3.15.
   - Synthesize `FiscalPeriod[]` (monthly, calendar-aligned to
     `Company.fiscal_year_start_month` / `_day`) per
     `blocks-financial-schema-design.md` §3.16. Set all periods to
     `Open` initially; period-close is a post-import user action.

2. **Tax jurisdictions + codes + rates.**
   - Load `TaxCategory.json` → synthesize one `TaxJurisdiction` per
     unique category (best-effort; flagged for user review).
   - Load `SalesTaxesAndChargesTemplate.json` +
     `PurchaseTaxesAndChargesTemplate.json` → each becomes a `TaxCode`.
   - For each template's `taxes` child rows → one `TaxRate` per row,
     linked to a `TaxJurisdiction` and the parent `TaxCode`.
   - Effective-date defaults to `2000-01-01` (covers history); user can
     refine post-import.

3. **Parties (Customer + Supplier + Contact + Address).**
   - Load `Customer.json` → for each:
     - Resolve `customer_type` → `Party.kind` (`Company` → org, `Individual` → person).
     - Create-or-update `Party` (canonical name = `customer_name`).
     - Create `PartyRole { partyId, roleKind: "customer", roleRecordId: <Customer.id> }`.
     - Create `Customer` extension record per `blocks-people-schema-design.md` §3.5:
       - `customerNumber = name` (ERPNext's stable id)
       - `arAccountId` = resolved from `Customer.default_receivable_account` via Pass 1 lookup (nullable; null is fine — chart's default AR is used at invoice time)
       - `defaultPaymentTermsId` = null (Anchor v1 has no payment-terms import; user re-sets post-import)
       - `taxExempt = false` (ERPNext doesn't carry exact equivalent; user re-flags post-import)
   - Load `Supplier.json` → analogous shape with `roleKind: "vendor"`.
   - Load `Contact.json` → for each contact:
     - If `Contact.links` references a Customer/Supplier already imported,
       attach the contact's email/phone to that Party as additional
       `EmailAddress`/`PhoneNumber` sub-entities.
     - Otherwise (orphan contact), create a bare Party with no role.
   - Load `Address.json` → for each address:
     - Walk `Address.links` to find the owning Customer/Supplier party.
     - Append as `PartyAddress` sub-entity. Set `isPrimary=true` if
       `Address.is_primary_address=1`.

4. **Cost-centers → Classifications (+ Property hints).**
   - Load `CostCenter.json`. For each cost-center, apply the §3.4
     heuristic to resolve to a `Property.id` or create a
     `Classification`.

**Commit boundary:** Each sub-pass commits its own SQLite transaction.
A failure in 2.3 (parties) does not roll back 2.1 (fiscal years).

**Failure modes:**
- Orphaned address with no parent party → reject-bin (skipped, logged).
- Unknown `customer_type` value → reject-bin.
- TaxCategory without any TaxRate → still imported; flagged in
  reconciliation report.

### 4.3 Pass 3 — Opening balances

**Purpose:** Establish the migration "as-of" position. ERPNext marks
opening entries with `is_opening = "Yes"` on the JournalEntry header.

**Steps:**

1. Load `JournalEntry.json` filtered to `is_opening == "Yes"`.
2. For each opening JE:
   - Map to Anchor `JournalEntry` with `sourceKind = "Migration"`.
   - Load corresponding `JournalEntryAccount` child rows.
   - Map each child row to a `JournalEntryLine` (validation §4.7 below).
   - Resolve `Account` references via Pass 1 results.
   - Validate `Σ debits == Σ credits` per the existing
     `JournalEntry` constructor (`blocks-accounting`/`blocks-financial-ledger`).
   - Insert via the canonical `postJournalEntry` algorithm
     (`blocks-financial-schema-design.md` §6.1). Atomic SQLite
     transaction. `sourceKind = "Migration"` bypasses the
     `entryDate <= today` rule (opening balances may be back-dated to
     the FY start).

**Commit boundary:** One SQLite transaction per opening JE. Partial
batch failure leaves some opening entries posted; reconciliation report
flags any missing ones.

**Failure modes:**
- Imbalanced opening JE → reject-bin
  (`OpeningEntryImbalanced { name, debitTotal, creditTotal }`); halts
  pass if the imbalanced amount exceeds the configured tolerance
  (default $0).
- References an account not imported in Pass 1 → halt
  (`UnknownAccountInOpeningEntry { name, accountName }`) — Pass 1 is
  expected to be complete.

### 4.4 Pass 4 — Transactional history

**Purpose:** Land the full transaction stream: invoices, bills,
payments, ordinary journal entries.

**Sub-passes (must run in this strict order):**

1. **Sales Invoices.**
   - Load `SalesInvoice.json` + `SalesInvoiceItem.json`.
   - Sort by `posting_date` ascending.
   - For each invoice:
     - Resolve `customer` → `Party.id` via Pass 2 results.
     - Resolve `debit_to` → `GLAccount.id` (the AR account) via Pass 1.
     - Build `Invoice` record per `blocks-financial-schema-design.md` §3.5.
     - Build `InvoiceLine[]` from `SalesInvoiceItem` children.
       De-itemize: `description = SalesInvoiceItem.description`;
       `incomeAccountId = SalesInvoiceItem.income_account` (resolved);
       `amount = SalesInvoiceItem.amount` (× 100 for minor units).
     - If `docstatus == 1`, transition to `Issued` (this triggers
       the JE-posting per §6.1 — but in migration mode, the JE is
       imported separately in Pass 4.4 below, so transition to
       `Issued` here **without** generating a new JE; instead, the
       importer attaches the corresponding ERPNext-imported JE in
       a post-pass linkage step).
     - If `docstatus == 2`, transition to `Voided` and link
       `voidedByEntryId` to the reversing JE imported in Pass 4.4.

2. **Purchase Invoices** → `Bill[]`. Analogous to Sales Invoices.

3. **Payment Entries** → `Payment[]` + `PaymentApplication[]`.
   - Load `PaymentEntry.json` + `PaymentEntryReference.json`.
   - For each `PaymentEntry`:
     - Map `payment_type` (`Receive`/`Pay`) → `direction`
       (`Inbound`/`Outbound`).
     - Map `mode_of_payment` (string) → `method` enum:
       - "Cash" → `Cash`
       - "Bank Draft" / "Check" / "Cheque" → `Check`
       - "ACH" / "Wire Transfer" → `ACH` / `Wire`
       - "Credit Card" → `Card`
       - else → `Other` (preserves original string in `notes`)
     - Resolve `paid_from` / `paid_to` → bank `GLAccount.id` based on
       direction.
     - Create `Payment` record per §3.9.
     - For each `PaymentEntryReference` child row, create a
       `PaymentApplication` per §3.10:
         - `appliedTo` = Invoice (Inbound) or Bill (Outbound).
         - `targetId` = resolved invoice/bill id.
         - `amountApplied` = child row's `allocated_amount` (× 100).
     - Compute `unappliedAmount` = total - Σ applications.

4. **Standalone JournalEntries** (any not already imported as opening,
   reversing, or invoice/bill/payment-derived).
   - Load `JournalEntry.json` filtered to `is_opening != "Yes"`.
   - Skip if this JE has already been imported via inverse-link
     (i.e., it's the JE-side of an already-imported invoice/bill/payment).
     The link is established via the `Sales Invoice.posting_date`
     matching the JE's `posting_date` AND a referenced JE name on the
     invoice (`Sales Invoice.journal_entries` field, if populated).
     If unlinked, the JE is treated as a standalone manual entry.
   - For each standalone JE:
     - Map to Anchor `JournalEntry` per §3.3 mapping.
     - Load `JournalEntryAccount` children.
     - Post via canonical `postJournalEntry` algorithm.

**Commit boundary:** Per-record SQLite transaction within each sub-pass.
Per-record failure goes to reject-bin and continues. Per-sub-pass
summary written at end.

**Failure modes:**
- Invoice references unknown customer → reject-bin
  (`UnknownCustomerOnInvoice { name }`).
- Payment references unknown invoice/bill → application is dropped
  (logged); payment is created as `Unapplied`.
- JE references unknown account → reject-bin
  (`UnknownAccountInJE { name, accountName }`).

### 4.5 Pass 5 — Reconciliation linkage

**Purpose:** Establish links between Payments and Invoices/Bills that
weren't already linked via `PaymentEntryReference` in Pass 4.3 (some
ERPNext setups have payments without explicit references).

**Steps:**

1. Load all `Payment` records imported in Pass 4 with `unappliedAmount > 0`.
2. For each, attempt heuristic match against open
   `Invoice` (Inbound) / `Bill` (Outbound) records:
   - Same party (`customerId` / `vendorId`).
   - Same date (within ±7 days configurable).
   - Exact amount match.
3. If unique match found → create `PaymentApplication`.
4. If ambiguous (>1 match) → leave unapplied; reconciliation report
   surfaces the candidates for user resolution.
5. If no match → leave as `Unapplied` payment.

**Commit boundary:** Single SQLite transaction.

### 4.6 Pass 6 — Verification + reconciliation report

**Purpose:** Prove the import preserved the financial position.

**Steps:**

1. **Trial-balance check.** For each chart, compute
   `Σ JournalLine.debit - Σ JournalLine.credit` grouped by `AccountType`.
   Expected: `Asset + Expense == Liability + Equity + Income`. Tolerance:
   $0 (no rounding leakage permitted; the integer-minor-units arithmetic
   guarantees this is exact when all lines balance per §6.1 algorithm
   invariant).

2. **AR aging check.** Compute Anchor's AR aging buckets per
   `blocks-financial-schema-design.md` §6.2 as-of the export date.
   Compare to a CO-prepared `ar-aging-snapshot.json` (optional file in
   the export root, produced one-time on the source ERPNext via
   `frappe.client.exec(...accounts_receivable...)`). Diff threshold:
   $0.01 per customer per bucket.

3. **AP aging check.** Symmetric.

4. **Per-account balance check.** For each `GLAccount`, sum its journal
   lines and compare to a CO-prepared `gl-balances-snapshot.json` (also
   optional but recommended). Diff threshold: $0.01 per account.

5. **Invoice balance reconciliation.** For each `Invoice` with status
   `Issued`/`PartiallyPaid`/`Paid`, compute
   `total - Σ applied + Σ writeoff` and assert it matches the imported
   `balance` field.

6. **Output: `migration-report.md`.** Written to
   `<export-root>/migration-report.md`. Sections:
   - Run summary (timestamp, pass durations, record counts per
     DocType).
   - Trial-balance verification result.
   - AR/AP aging diff tables.
   - Per-account balance diff (sorted by absolute difference).
   - Reject-bin summary (record counts by reject-reason).
   - Unapplied-payment list.
   - Cost-center → Property/Classification resolution summary.
   - Any warnings (unknown voucher_type, unmapped DocType files in
     `_unmapped/`, etc.).

**Commit boundary:** Read-only. No writes (the report is the only
output).

**Failure modes:**
- Trial balance fails to zero → halt with
  `TrialBalanceMismatch { diff }`. This is a hard halt — the importer
  rolls back the entire run and surfaces the report. (In practice, if
  Pass 4 ran without imbalanced-JE rejections, this cannot fail; the
  check is a defense-in-depth invariant.)
- AR/AP aging diff exceeds threshold → halt with
  `AgingReconciliationFailed { perCustomerDiffs }`. CO resolves
  (either fix source data or accept the diff via
  `--allow-aging-drift` flag).

### 4.7 JournalEntryLine field mapping (referenced across passes)

```
ERPNext JournalEntryAccount field         →  Anchor JournalEntryLine field
─────────────────────────────────────────────────────────────────────────
account                                   →  accountId (resolved)
debit_in_account_currency                 →  debit.amount × 100 (minor units)
credit_in_account_currency                →  credit.amount × 100 (minor units)
cost_center                               →  propertyId (if resolved) OR classificationId
user_remark                               →  memo
party_type + party                        →  (stored as note context; not on line in v1)
reference_type + reference_name           →  used to drive Sales Invoice / Purchase Invoice / Payment Entry linkage in Pass 4
```

---

## 5. Idempotency contract

### 5.1 External-reference shape

Every imported entity carries:

```ts
externalRef: {
  source: "erpnext",
  id: string,          // ERPNext "name" — stable, unique within DocType
  version: string,     // ERPNext "modified" — RFC 3339-ish timestamp
}
```

In SQLite, this is stored as three columns: `external_ref_source`,
`external_ref_id`, `external_ref_version`, with a composite index
`(external_ref_source, external_ref_id)` for the idempotency lookup.

### 5.2 Re-run semantics

On re-run, for each source record:

1. Look up by `(externalRef.source, externalRef.id)`.
2. If absent → insert (counts as "new").
3. If present and `externalRef.version` equals source's `modified` →
   skip (counts as "unchanged"; no SQL UPDATE issued).
4. If present and source's `modified` is **strictly greater** →
   update fields (counts as "updated"). For posted JournalEntries
   (which are immutable post-post in Anchor), an update is FORBIDDEN
   — the importer surfaces a warning and leaves the existing record
   intact (`JournalEntryUpdateAttemptOnImmutable { name }`).
5. If present and source's `modified` is **strictly less** (clock
   drift / re-export of older data) → warn and skip.

### 5.3 Deletion is not supported

The importer never deletes Anchor records based on a missing source
record. If a record was deleted in ERPNext, it remains in Anchor with
its last-known state. The migration-report.md surfaces this divergence
in a separate section.

### 5.4 Re-run consistency invariants

- The set of `(externalRef.source, externalRef.id)` keys is monotonic
  across re-runs (only grows or stays equal).
- The total `Σ debit - Σ credit` per chart is invariant across re-runs
  (zero, always).
- AR/AP totals across re-runs may grow (new transactional records
  imported) but never shrink unless source records were deleted —
  which surfaces as a warning, not a write.

---

## 6. Error handling

### 6.1 Manifest validation (pre-flight)

Before any pass runs, the importer validates the export directory:

- `manifest.json` exists and is valid JSON.
- `manifest.json.erpnextVersion` is in the supported range (v14 / v15;
  validated by major-version string).
- Every required file (per §2.2 table) is present OR explicitly listed
  as `skipped` in `manifest.json.skippedFiles[]`.
- Every file is valid JSON (top-level array of objects).

A failure here aborts the import with `ManifestValidationFailed { reasons }`.
No SQLite writes occur.

### 6.2 Per-pass commit semantics

| Pass | Commit boundary | Failure semantics |
|---|---|---|
| 1 (Chart) | Single transaction over all accounts | Roll back; no accounts persisted |
| 2 (Reference data) | Sub-pass per data family (4 sub-transactions) | Sub-pass failure leaves earlier sub-passes intact |
| 3 (Opening) | Per-JE transaction | Per-record reject; pass completes if any opening JE succeeds |
| 4 (Transactional) | Per-record transaction per sub-pass | Per-record reject; sub-pass completes |
| 5 (Reconciliation) | Single transaction | Roll back; no reconciliation links persisted |
| 6 (Verification) | No writes | N/A |

The rationale: Pass 1 + Pass 5 are small, atomic operations where
partial-success is meaningless. Passes 3/4 are large, where
per-record progress is preferable to all-or-nothing.

### 6.3 Reject-bin

Every record that fails validation is written to a reject-bin row in
the `migration_audit_log` SQLite table (separate from the main
record tables):

```sql
CREATE TABLE migration_audit_log (
  id              TEXT PRIMARY KEY,         -- ULID
  run_id          TEXT NOT NULL,            -- ULID per import run
  pass            TEXT NOT NULL,            -- "pass1", "pass2.1", ...
  doctype         TEXT NOT NULL,            -- "Account", "Sales Invoice", ...
  source_id       TEXT NOT NULL,            -- ERPNext name
  outcome         TEXT NOT NULL,            -- "inserted" | "updated" | "skipped" | "rejected"
  reject_reason   TEXT,                     -- nullable; structured code
  reject_detail   TEXT,                     -- nullable; JSON blob with payload
  created_at_utc  TEXT NOT NULL             -- RFC 3339
);
CREATE INDEX migration_audit_run ON migration_audit_log(run_id);
CREATE INDEX migration_audit_doctype ON migration_audit_log(doctype, outcome);
```

The reject-bin is queried by the migration-report.md generator in Pass 6.

### 6.4 Partial-failure recovery

If a pass aborts mid-way (e.g., Pass 1 cycle detection halts), the user:

1. Reads `migration-report.md` for the diagnostic.
2. Fixes the source data (rare) OR adds a CLI flag override OR adjusts
   `property-aliases.json`.
3. Re-runs `anchor import erpnext --source <export-dir>`.
4. The idempotency contract (§5) ensures already-imported records are
   skipped; only the failed pass re-runs.

### 6.5 Logging

Every record outcome (insert/update/skip/reject) is logged to:

- The `migration_audit_log` table (structured, query-able post-run).
- Stderr (line-per-record, throttled to 1-per-100 for large passes;
  every reject is logged).
- A run-scoped log file at
  `<anchor-data-dir>/migration-runs/<run-id>/import.log`.

---

## 7. Acceptance tests

These are the gate criteria for declaring the importer "shippable" per
ADR 0088 phase 1 acceptance.

### 7.1 Per-record correctness

- **A1.** Every ERPNext `Account` record in the Acero export results in
  exactly one `GLAccount` row in Anchor with `externalRef.id == Account.name`.
- **A2.** Every ERPNext `Journal Entry` with `docstatus == 1` results in
  exactly one `JournalEntry` in Anchor with status `Posted`, and the
  imported `JournalEntryLine` count matches the source's
  `JournalEntryAccount` child count.
- **A3.** Every ERPNext `Sales Invoice` with `docstatus == 1` results in
  exactly one `Invoice` in Anchor with status `Issued`/`PartiallyPaid`/`Paid`
  (based on `outstanding_amount`).
- **A4.** Every ERPNext `Payment Entry` with `docstatus == 1` results in
  exactly one `Payment` in Anchor with status `Cleared`/`Applied`/etc.,
  and `Σ PaymentApplication.amountApplied` equals the source's
  `Σ PaymentEntryReference.allocated_amount`.

### 7.2 Aggregate correctness ($7.6M Wave history preservation)

- **B1. Trial balance.** Post-import per-chart:
  `|Σ debit - Σ credit| == 0`. (Hard zero — integer minor units.)
- **B2. Total AR.** Post-import: `Σ Invoice.balance where status in
  ('Issued','PartiallyPaid')` equals the ERPNext snapshot's total AR
  within $0.01 tolerance per LLC.
- **B3. Total AP.** Symmetric.
- **B4. Period P&L (test scope: FY 2024).** Post-import: per-account
  sum of `Revenue` lines minus sum of `Expense` lines for FY 2024
  equals ERPNext-reported P&L for FY 2024 within $0.01 per chart.
- **B5. Wave-history-migrated $7.6M total.** Sum of all opening
  balances (Pass 3) across all 4 LLCs equals the Wave-history total
  within $1.00 (looser threshold acknowledging FX/rounding in the
  Wave→ERPNext migration that preceded this one).
- **B6. AR aging match.** Per `blocks-financial-schema-design.md`
  §6.2 algorithm: per-customer aging buckets match ERPNext aging
  snapshot within $0.01 per customer per bucket.

### 7.3 Idempotency

- **C1.** Running the importer twice against the same export
  produces zero new records on the second run (every record's
  outcome is `skipped` with reason `version-unchanged`).
- **C2.** Running the importer against an export where exactly one
  source record's `modified` timestamp moved forward produces
  exactly one `updated` outcome.
- **C3.** Running the importer against an export with an additional
  record (vs. the previous run) produces exactly one `inserted`
  outcome for that record.

### 7.4 Reject-bin sanity

- **D1.** An import-run with deliberately-corrupted Account.json
  (a cyclic parent_account chain) halts Pass 1 with no GLAccount rows
  persisted and writes a `CyclicAccountTree` reject entry.
- **D2.** An import-run with one orphaned Address (links a non-existent
  Customer) produces exactly one reject-bin entry and completes
  successfully (does not halt the pass).

### 7.5 Reconciliation report shape

- **E1.** `migration-report.md` exists in `<export-root>/` post-run.
- **E2.** Report contains the seven sections enumerated in §4.6 step 6.
- **E3.** Per-account balance section is sorted by absolute diff
  descending.

### 7.6 Property-occupancy check (cross-cluster sanity)

- **F1.** If the export includes a custom `Property` DocType, the
  Anchor `Property` count post-import equals the ERPNext count.
- **F2.** If `property-aliases.json` resolves all CO-prepared
  cost-center → property mappings, the
  `migration-report.md` Cost-center section shows zero unresolved
  cost-centers.

---

## 8. CLI / UX surface

### 8.1 Command line

```bash
anchor import erpnext --source <export-dir> [options]
```

**Required:**
- `--source <path>` — path to the export directory (per §2.2).

**Optional:**
- `--target-chart <legalEntityId>` — restrict import to one company.
  Default: import all companies in `manifest.json.companies`.
- `--dry-run` — runs all six passes against a temporary in-memory
  SQLite, produces the migration-report.md, and writes nothing to the
  actual Anchor data file. The user inspects the report before
  committing.
- `--allow-aging-drift` — accept Pass 6 AR/AP aging diffs over the
  threshold without halting. The diff is still recorded in the report.
- `--allow-multi-currency-skip` — skip rather than reject
  transactional records whose currency doesn't match the chart's
  baseCurrency. Default is to reject (so the user sees a clear count
  of skipped records).
- `--from-pass <N>` — resume from pass N (1..6). Default: 1.
  Useful for re-runs where Pass 1 already succeeded.
- `--reject-threshold <N>` — halt the run if any pass produces more
  than N rejects. Default: unlimited.
- `--verbose` — verbose stderr logging (full per-record outcome).
  Default: progress-only (one line per pass / per sub-pass).

### 8.2 Progress UX (terminal)

```
Anchor Migration Importer (ERPNext → Anchor)
============================================
Source: /Users/cw/erpnext-export-2026-05-16
Target: 4 charts (Acero, Bosco, Escola, Shirin)

Manifest validation................ OK
Pass 1 (Chart of accounts)......... ████████████████████ 100% (1,247 accounts)
Pass 2 (Reference data)............ ████████████████████ 100%
  2.1 Fiscal years + periods....... OK (5 FYs; 60 periods)
  2.2 Tax codes + rates............ OK (8 codes; 23 rates)
  2.3 Parties...................... OK (412 parties)
  2.4 Cost-centers................. OK (37 cost-centers; 28 → properties via alias map)
Pass 3 (Opening balances).......... ████████████████████ 100% (4 entries)
Pass 4 (Transactional history)
  4.1 Sales Invoices............... ████████████████████ 100% (2,847 invoices)
  4.2 Purchase Invoices............ ████████████████████ 100% (1,103 bills)
  4.3 Payment Entries.............. ████████████████████ 100% (3,201 payments)
  4.4 Standalone JEs............... ████████████████████ 100% (89 entries)
Pass 5 (Reconciliation linkage).... OK (12 heuristic matches; 4 unapplied remaining)
Pass 6 (Verification).............. OK
  Trial balance: BALANCED (|diff| = 0.00)
  Total AR diff: $0.00 (Acero), $0.00 (Bosco), $0.00 (Escola), $0.00 (Shirin)
  Total AP diff: $0.00 (Acero), $0.00 (Bosco), $0.00 (Escola), $0.00 (Shirin)
  FY 2024 P&L diff: $0.00 (all charts)

Run summary written to: /Users/cw/erpnext-export-2026-05-16/migration-report.md
```

Each pass writes a single status line on completion; in-progress
percentage is updated in-place via a TTY repaint (falls back to
periodic line-rewrite on non-TTY stdout).

### 8.3 Anchor desktop integration (post-CLI)

A future PR (not in Phase 1 scope) wraps the CLI as a one-time
onboarding wizard in the Anchor Tauri-React shell:

- Step 1: "Have you been using ERPNext? Point us at your export folder."
- Step 2: dry-run preview with the seven-section report rendered inline.
- Step 3: "Looks good?" → commit (calls the same CLI logic without
  `--dry-run`).
- Step 4: success screen with link to migration-report.md.

The wizard surface is a separate workstream; the CLI is the canonical
entry point and the import logic is the same.

---

## 9. Clean-room implementation discipline

Per ADR 0088 §3 (mandatory clean-room discipline for copyleft sources)
and §11.5 of `blocks-financial-schema-design.md`.

### 9.1 What the importer reads from ERPNext

**Data only.** The export files are JSON arrays following ERPNext's
documented export schema. The schema itself is a public data
interchange contract (documented at <https://docs.erpnext.com/>; the
field names appear in ERPNext's user-facing UI as column headers and
in API docs).

### 9.2 What the importer NEVER does

- **Does not import ERPNext / Frappe Python code.** No `from frappe import`,
  no `from erpnext.X import`, no copy-paste of validator logic, no
  re-implementation of controller code.
- **Does not vendor ERPNext fixtures or example data.** The importer is
  data-format-aware; it doesn't ship with ERPNext example data baked in.
- **Does not read Frappe DocType definition files** (the `.json`
  metadata files that declare DocType field types). The field names
  are baked into the importer code via §3's mapping table, derived
  from the public docs / observed export shape.

### 9.3 What the importer DOES use (permissive sources)

- **Standard JSON parsing** — language built-in.
- **SQLite library** — standard library / `Microsoft.Data.Sqlite`.
- **`postJournalEntry` algorithm** — clean-room work per
  `blocks-financial-schema-design.md` §6.1 (textbook double-entry;
  500-year-old patterns).
- **Topological sort** — textbook algorithm (Kahn's / Tarjan's); not
  derived from Frappe.
- **OFBiz attribution** (permissive) — the cluster-level entity
  shapes (`GLAccount`, `Invoice`, `Bill`) derive from OFBiz's
  `accounting` module (Apache 2.0); the importer simply targets
  those shapes.

### 9.4 Reading isolation

ERPNext source studied during the design of this importer was studied
in a separate git worktree (not in the Sunfish repo), in editor
sessions not connected to any Sunfish file. The mapping tables in §3
were derived from observed export files plus ERPNext's public user
docs (DocType field labels as users see them). No DocType-definition
JSON files (Frappe's internal schema) were referenced.

### 9.5 Attribution

The importer's source-header comment names ERPNext + Frappe + the
GPLv3 license + "format-reference-only; no code derived" disclaimer,
mirroring the ADR 0088 §3.4 attribution pattern (adapted for
copyleft-format-reference rather than permissive-code-borrow).

---

## 10. Open questions (CO / cob ratification)

These are flagged for CO/cob review before Stage 06 build of the
importer itself.

### 10.1 Property/Lease custom-DocType assumption

The CO's Mac-ERPNext instance may or may not have custom `Property` /
`Lease` DocTypes installed. The importer must handle both:

- **If present** — direct mapping (DocType-to-entity) per §3.1.
- **If absent** — `Cost Center` heuristic (§3.4) + manual
  property entry post-import.

**Question:** Does CO's instance have these custom DocTypes? If not,
the alias-map shape needs to be confirmed pre-cutover.

### 10.2 Multi-currency v1 posture

Anchor v1 is single-currency per chart. If any of the 4 LLCs has
transactional records in a non-USD currency (rare for VA-located rental
real estate but possible if there was, e.g., a CAD vendor bill), the
importer rejects them per §3.5.

**Question:** Is this acceptable, or should v1 also support
multi-currency for migration even if Anchor's runtime is
single-currency v1? (Recommendation: reject + log; user re-enters in
USD post-import if needed. Defer multi-currency to v2.)

### 10.3 Payment-application heuristic threshold

Pass 5 heuristically matches unapplied payments to invoices/bills by
party + amount + date (±7 days). What if the threshold needs tuning?

**Question:** Ship with ±7 days as default and `--date-tolerance <N>`
flag to override? (Recommendation: yes.)

### 10.4 `Cost Center` ambiguity

When a cost-center matches multiple properties (e.g., "Acero" maps to
3 properties), the heuristic falls through to `Classification`. Should
there be a strict-vs-best-effort flag?

**Question:** Add `--cost-center-strict` flag that halts on ambiguity
instead of falling through? (Recommendation: yes; default off.)

### 10.5 Wave-history reconciliation tolerance

§7.2 B5 sets a $1.00 tolerance on the Wave-history-migrated $7.6M
total. Is this loose enough (acknowledging the Wave→ERPNext
migration's own rounding) or too loose (potentially masking a real
defect)?

**Question:** Confirm $1.00 vs e.g. $10.00. (Recommendation: keep
$1.00; CO has the Wave-side audit trail and can tighten if desired.)

### 10.6 ERPNext export-script ownership

The export-production commands in §2.1 must be authored on the
Mac-ERPNext side. Is this:

- (a) A CO one-time task with documented commands in the
  migration-report.md preamble? (Recommendation.)
- (b) An importer-owned task — the importer SSHes into the source
  ERPNext via `bench`? (Rejected — coupling to live Frappe runtime
  defeats the clean-room read-only-against-export design.)

### 10.7 `blocks-rent-collection` wrapper sequence

Per the naming ratification, existing `blocks-rent-collection.Invoice`
becomes a non-breaking wrapper over the canonical financial-AR
`Invoice` once that lands. The importer creates financial-AR
Invoices directly; rent-collection wrapper records are *not* created
by the importer (they're a runtime-side wrapper, not a persistence
shape).

**Question:** Confirm the importer skips `rent-collection.Invoice`
creation and leaves rent-collection-side records to runtime issuance
post-migration? (Recommendation: yes.)

### 10.8 Pass-3 opening-balance source-kind ambiguity

ERPNext marks opening JEs with `is_opening = "Yes"`. Some property
owners' books may have multiple opening-entry waves over time (e.g.,
yearly opening entries from the prior accountant). Should every
`is_opening` JE be tagged `sourceKind = "Migration"`, or only the
single earliest one?

**Question:** Recommendation: tag every `is_opening` JE as
`Migration`. Anchor's `sourceKind` enum doesn't distinguish
opening-balance-wave; the `entryDate` field disambiguates.

### 10.9 Reject-bin disposition for the production cutover

Post-cutover, the reject-bin may contain dozens-to-hundreds of
records that the CO didn't get a chance to fix on the ERPNext side
(orphan addresses, mis-mapped tax categories, etc.). What's the
remediation flow?

**Question:** Recommendation: ship a `anchor import erpnext review-rejects`
sub-command that lists rejects + offers per-record resolution UI
(skip permanently / fix-and-retry / accept-as-warning). Not in
Phase 1 scope but flagged for Phase 1.5 polish.

### 10.10 Performance budget

For CO's 4-LLC portfolio (estimated ~10K total records across all
DocTypes), the importer should complete in <5 minutes on a Surface
Pro 7 class machine.

**Question:** Confirm the 5-minute budget? (Recommendation: yes;
profile during Phase 1 build and surface any pass that exceeds
30 seconds.)

---

## 11. Cross-cluster dependencies (importer spans 5 clusters)

The importer is the only Phase 1 deliverable that crosses every
cluster except `blocks-work-*`, `blocks-docs-*`, and
`blocks-storefront-*`:

| Cluster | What the importer creates | Stage 02 design doc |
|---|---|---|
| `blocks-financial-ledger` | `GLAccount`, `JournalEntry`, `JournalEntryLine` | `blocks-financial-schema-design.md` §3.1–§3.4 |
| `blocks-financial-ar` | `Invoice`, `InvoiceLine`, `Receipt` | §3.5–§3.6, §3.11 |
| `blocks-financial-ap` | `Bill`, `BillLine` | §3.7–§3.8 |
| `blocks-financial-payments` | `Payment`, `PaymentApplication` | §3.9–§3.10 |
| `blocks-financial-tax` | `TaxCode`, `TaxRate`, `TaxJurisdiction` | §3.12–§3.14 |
| `blocks-financial-periods` | `FiscalYear`, `FiscalPeriod` | §3.15–§3.16 |
| `blocks-financial-chart` | `Classification` (via cost-center heuristic) | §3.19 |
| `blocks-people-*` | `Party`, `PartyRole`, `EmailAddress`, `PhoneNumber`, `PartyAddress`, `Customer`, `Vendor` | `blocks-people-schema-design.md` §3.1, §3.5 |
| `blocks-property-*` (optional) | `Property`, `Lease` (only if custom DocTypes present) | (existing 14 intakes from 2026-04-28) |
| `blocks-reports-*` | `MigrationAuditLog` (the migration-run record), `TaxFormLineMap` (NOT written by importer; future seed) | `blocks-reports-schema-design.md` §8.2 |

**Sequencing constraint:** every cluster's chart-of-accounts + journal
entity must land before the importer can run. Per the Phase 1
hand-off chain, the order is:

1. `blocks-financial-ledger` rename + extensions (this is the
   first Stage 06 hand-off — see
   `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md`).
2. `blocks-financial-tax` + `-periods` + `-ar` + `-ap` +
   `-payments` (parallel-able after ledger lands).
3. `blocks-people-*` Party + Customer + Vendor extensions.
4. `blocks-property-*` Property + Lease (existing 14 intakes
   in-flight).
5. **Importer itself** — depends on (1)–(4) being available.

The importer can be authored and unit-tested against in-memory test
doubles before (1)–(4) land; integration tests against real Anchor
storage gate-on the dependencies.

---

## 12. Out-of-band note — relationship to existing `blocks-rent-collection`

Per the naming-ratification ruling (Decision 3), the existing
`blocks-rent-collection.Invoice` + `Payment` types become non-breaking
wrappers over the canonical financial-AR `Invoice` + `Payment` once
those land. The importer does **not** create rent-collection-side
wrapper records; it writes the canonical financial-AR types directly.

Rent issuance going forward (post-migration) flows:

```
rent-collection.RentSchedule.issue()
  → creates financial-AR.Invoice (the canonical record; importer-compatible)
  → creates rent-collection.Invoice (thin wrapper; preserves
                                     rent-specific generation logic;
                                     references the financial-AR.Invoice by id)
```

The importer is unaware of the wrapper layer — it produces records the
wrapper layer can reference but doesn't generate wrapper records. This
keeps the importer's responsibility scoped to the canonical entities.

---

## 13. Versioning + future extensions

This document is **v1**. Anticipated future versions:

- **v1.1** — multi-currency import (post-Phase-3 Anchor multi-currency
  support).
- **v1.2** — incremental import (delta-only, post initial cutover) for
  the rare case where CO keeps a small ERPNext shard running in
  parallel during a phased cutover.
- **v2** — adapt to ERPNext v16+ export-format changes (if any).
- **v2** — sibling adapters (`anchor import wave`,
  `anchor import quickbooks`, `anchor import xero`) following the
  same 6-pass pattern with cluster-specific source mappings.

The 6-pass shape is intended to be re-used for any source-system
adapter; only Pass 1 + Pass 2 mapping tables are source-specific.

---

## 14. References

- ADR 0088 — *Anchor as All-In-One Local-First Runtime*
  ([`docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`](../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md))
- Naming-ratification ruling
  (`coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`)
- Stage 02 — `blocks-financial-schema-design.md` (esp. §3, §6, §7, §10, §11)
- Stage 02 — `blocks-people-schema-design.md` (esp. §3.1, §3.5)
- Stage 02 — `blocks-reports-schema-design.md` (esp. §8)
- Stage 02 — `blocks-property-*` 14 intakes from 2026-04-28
  (`icm/00_intake/property-ops-INDEX-intake.md` and siblings)
- ERPNext public docs — <https://docs.erpnext.com/> (data shapes only;
  no code referenced)
- IRS Publication 527 — *Residential Rental Property* (public domain;
  informs `Property`/`Lease` shapes in `blocks-property-*`)

---

**End of document.**
