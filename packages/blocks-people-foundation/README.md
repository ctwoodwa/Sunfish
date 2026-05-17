# Sunfish.Blocks.People.Foundation

Canonical **Party** substrate for Sunfish. Every other block that needs to talk about humans or organizations — leases, accounts receivable / payable, work orders, comms — attaches roles to a `Party` defined here, instead of carrying its own customer / tenant / vendor table.

## PR 1 scope (this commit)

Entities + validation only. No services, no DI, no repository, no event emission.

| File group | What ships |
|---|---|
| `Models/PartyId.cs`, `EmailAddressId.cs`, `PhoneNumberId.cs`, `PartyAddressId.cs` | Four strongly-typed identifier record-structs with `JsonConverter`. Backed by `Guid` per current Sunfish ID convention (see `TaxCodeId`, `GLAccountId`). The hand-off proposed ULID; that's deferred pending a repo-wide sweep so all aggregate roots flip together. |
| `Models/PartyKind.cs` | `enum PartyKind { Person, Organization }` + JSON converter persisting as lowercase string codes. |
| `Models/Address.cs` | Flat postal-address value object. Country is ISO 3166-1 alpha-2. |
| `Models/Party.cs` | The canonical Party aggregate. Person-shaped + organization-shaped fields collapsed onto one record per party-model-convention §3. Includes the standard CRDT envelope (`CreatedAt/By`, `UpdatedAt/By`, `DeletedAt/By`, `Version`, `RevisionVector`). |
| `Models/EmailAddress.cs`, `PhoneNumber.cs`, `PartyAddress.cs` | Append-only contact-info rows keyed by `PartyId`. Updates are modeled as "insert new + mark old `ReplacedAt`" per CRDT-friendly-schema-conventions §4 — never UPDATE in place. |
| `Validation/PartyValidator.cs` | Kind-coherent name rules + the `ParentOrgId` only-on-orgs guard. |
| `Validation/EmailAddressValidator.cs` | RFC 5322 via `System.Net.Mail.MailAddress`; rejects display-name forms so `Address` stays a bare addr-spec. |
| `Validation/PhoneNumberValidator.cs` | Strict E.164 (`^\+[1-9]\d{1,14}$`). |
| `Validation/PartyAddressValidator.cs` | ISO 3166-1 alpha-2 country shape + chronological ordering of `ValidFrom`/`ValidTo`. |

**Tests:** 27 across `PartyTests` / `EmailAddressTests` / `PhoneNumberTests` / `PartyAddressTests`.

## What's NOT in PR 1

- **Role registry** (PR 2) — `PartyRole` + customer / tenant / vendor / contractor / employee.
- **Read/write services + repository** (PR 3) — `IPartyReadModel`, `IPartyWriteService`, `InMemoryPartyRepository`, `AddBlocksPeopleFoundation()` DI extension. PR 3 also wires the `IActorPrincipalResolver` boundary (Halt 1).
- **ERPNext importer + docs page** (PR 4) — `IErpnextPartyImporter` mapping Customer / Supplier doctypes to `Party` + role, and `apps/docs/blocks-people-foundation/overview.md`.

## Conventions referenced

- `apps/docs/conventions/party-model-convention.md` — entity shape rationale.
- `apps/docs/conventions/crdt-friendly-schema-conventions.md` §4 — append-only with `ReplacedAt`.
- Stage 02 design — Party substrate + role-edge model.
- ADR 0088 — Anchor all-in-one local-first runtime (data-locality target).

## PII posture

`Party.TaxId` and `Party.DateOfBirth` ship **UNENCRYPTED** in v1, marked with `// TODO: encrypt-at-rest per W#37/ADR-0068`. The encryption pass wraps them at the persistence boundary once Stronghold / OS-keychain support lands (already in flight on the Anchor side) and ADR 0068 reaches Accepted. Public API surface does NOT change.

## Build + test

```bash
dotnet build packages/blocks-people-foundation/Sunfish.Blocks.People.Foundation.csproj
dotnet test  packages/blocks-people-foundation/tests/Sunfish.Blocks.People.Foundation.Tests.csproj
```
