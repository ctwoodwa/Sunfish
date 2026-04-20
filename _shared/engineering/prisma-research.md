# Prisma ORM — Technical Research for Sunfish / Bridge

**Version covered:** Prisma 6.x (latest: 6.19.2); Prisma 7 breaking changes documented  
**Evaluated:** 2026-04-20  
**Scope:** Lessons learned + patterns applicable to Bridge's .NET data layer; direct adoption not applicable (.NET only)

---

## Summary

Prisma is a schema-first, type-safe data access layer for Node.js/TypeScript. It is not directly adoptable in a .NET/Blazor context, but its architectural patterns address well-known ORM pain points that are equally relevant to Entity Framework Core. The lessons below are the high-value takeaways for Sunfish Bridge's C# data layer.

**Direct adoption path:** If Bridge ever builds a Node.js BFF layer (e.g., for Next.js SSR features), Prisma is a strong candidate for PostgreSQL or SQL Server access.

---

## Core Architecture

### Four Distinct Layers

| Layer | What it is |
|---|---|
| **Prisma Schema Language (PSL)** | `schema.prisma` — single source of truth for models, relations, generators, and datasource |
| **Query Engine** | Historically a Rust binary; v6 adds `engineType: "client"` pure-JS/WASM engine for edge runtimes |
| **Client Generator** | `prisma generate` reads the schema, produces a fully-typed TypeScript client |
| **`prisma.config.ts`** | v6+ — separates connection config/secrets from schema definitions |

```prisma
// schema.prisma
generator client {
  provider   = "prisma-client"
  output     = "./generated/prisma"
  engineType = "client"           // pure JS — no Rust binary
}

datasource db {
  provider = "postgresql"
  // URL now in prisma.config.ts, not here in v6+
}

model User {
  id    Int     @id @default(autoincrement())
  email String  @unique
  posts Post[]
}
```

```typescript
// prisma.config.ts (v6+)
import "dotenv/config";
import { defineConfig, env } from "prisma/config";

export default defineConfig({
  schema: "prisma/schema.prisma",
  migrations: { path: "prisma/migrations" },
  datasource: { url: env("DATABASE_URL") },
});
```

---

## Version History — Key Changes

### Prisma 5 → 6
- `prisma.config.ts` introduced (optional in v6, mandatory in v7)
- `omit` field exclusion reached GA (exclude sensitive fields per-query or globally)
- Default generator output moved to `./generated/prisma` (not `node_modules/@prisma/client`)
- `engineType: "client"` — pure-JS engine (no native binary) made available

### Prisma 6 → 7 (Breaking)
- `--schema` and `--url` CLI flags **removed** — all config must be in `prisma.config.ts`
- Migration diff CLI flags changed (`--from-url`, `--to-url` removed; replaced by `--from-config-datasource`)
- Scripts passing `--schema=./path` to CLI commands will fail without migration

---

## Type Safety — How the Generated Client Works

The return type of a query is structurally derived from what you `select` or `include`:

```typescript
// Returns User & { posts: Post[] }
const user = await prisma.user.findUnique({
  where: { id: 1 },
  include: { posts: true }
});

// Returns { email: string } — NOT a full User
const slim = await prisma.user.findUnique({
  where: { id: 1 },
  select: { email: true }
});

// Exclude sensitive fields (v6.2.0+)
const users = await prisma.user.findMany({
  omit: { password: true }
});
```

**Key difference from EF Core:** Relations are lazy by default in EF Core (with navigation proxy proxies). In Prisma, relations are **never loaded unless explicitly requested via `include` or `select`** — no lazy-loading traps, no accidental N+1 via navigation properties.

---

## Migration Workflow

### `prisma migrate dev` (development only)

1. Creates a **shadow database** (temporary second DB, same provider)
2. Replays migration history into shadow DB
3. Detects **schema drift** (manual DB edits not in migration history)
4. Generates new `.sql` migration files for schema changes
5. Applies migration to dev database

Migration files are **plain SQL committed to source control** — reviewable in PRs without running the app.

### `prisma migrate deploy` (production / CI)

- Applies pending `.sql` migration files sequentially
- No drift detection, no shadow database, no resets
- Creates the database if it doesn't exist
- Safe to run before application startup in CI/CD

### `prisma migrate resolve`
For partially-failed production migrations — marks a migration as applied or rolled back in the `_prisma_migrations` tracking table without re-running SQL.

---

## Multi-Database Support

| Provider | Status | Notes |
|---|---|---|
| `postgresql` | Full | First-class |
| `sqlserver` | Full | Production-ready, important for .NET enterprise |
| `mysql` | Full | |
| `sqlite` | Full | Dev/test; no shadow DB needed |
| `cockroachdb` | Full | |
| `mongodb` | Partial | **No Prisma Migrate** — schema-push only via `prisma db push`; no migration history |

**SQL Server is fully supported** — connection string format uses semicolon-delimited key=value rather than a URI.

---

## Prisma vs. Entity Framework Core

| Dimension | Prisma (TypeScript) | Entity Framework Core (.NET) |
|---|---|---|
| Schema source of truth | `schema.prisma` (SDL — one file) | C# entity classes + Fluent API + annotations (distributed) |
| Lazy loading | ❌ Not supported — structural prevention | Optional (proxy-based); common source of N+1 |
| Change tracking | None — stateless client | Full change tracker on DbContext (Unit of Work) |
| Query model | Fluent object API | LINQ |
| Return type shape | Derived structurally from `select`/`include` at compile time | Entity type always (unless projected with `.Select()`) |
| Migration format | Plain `.sql` files, committed | C# migration classes (generate SQL at runtime) |
| Schema drift detection | Built into `migrate dev` via shadow DB | Manual (`dotnet ef migrations has-pending-model-changes`) |
| Raw SQL type safety | `unknown` — must annotate manually | Compile-time via `FromSqlRaw` with typed entities |
| Cross-database joins | ❌ One datasource per schema | Possible with multiple DbContexts |
| Interactive transactions | `$transaction(async tx => {...})` | `BeginTransactionAsync()` |

**Philosophical difference:** Prisma = stateless Data Mapper. EF Core = Unit of Work with change tracking. Prisma is more predictable; EF Core is more convenient for update-heavy workflows.

---

## Prisma Accelerate and Pulse

**Prisma Accelerate** — managed connection pooler + global query cache:
- Routes edge function queries through Prisma's hosted HTTP proxy to a persistent connection pool
- Per-query TTL caching: `cacheStrategy: { ttl: 60, swr: 30 }`
- Raw queries bypass the cache
- **Commercial SaaS** — no self-hosting option

**Prisma Pulse** — real-time CDC (change data capture) as a service:
- Surfaces DB change events as async subscription streams
- Also **commercial SaaS**
- .NET equivalent: SQL Server CDC, PostgreSQL logical replication, Azure Service Bus

---

## Lessons for Bridge's .NET Data Layer

These are the patterns Prisma enforces by design that translate directly to EF Core:

### A. Schema as Single Source of Truth
Prisma has one `schema.prisma` file. EF Core spreads definitions across entity classes, Fluent API in `OnModelCreating`, and data annotations.

**Recommendation:** Enforce a single `IEntityTypeConfiguration<T>` configuration class per entity. Never use data annotations on entity classes themselves. This creates a Prisma-equivalent "one place to look" for the model definition.

### B. Disable Lazy Loading Everywhere
Prisma structurally prevents N+1 by making lazy loading impossible.

**Recommendation:** Disable lazy loading in all Bridge `DbContext` instances. Require explicit `.Include()` — identical discipline to Prisma's `include: { ... }`. Make this a CI gate (enable a Roslyn analyzer or custom rule to detect `.LazyLoadingEnabled = true`).

### C. Migrations as Plain SQL, Reviewed in PRs
Prisma generates `.sql` files automatically. EF Core generates C# migration classes that produce SQL at runtime.

**Recommendation:** Always run `Script-Migration` in EF Core to generate SQL scripts for DBA review before production deploys. This is equivalent to Prisma's automatic SQL migration file generation. The SQL script should be a CI artifact and reviewed in the PR.

### D. Project to DTOs, Never Return Full Entities
Prisma's `select` returns only what you ask for, with a type matching exactly that shape. EF Core returns full entity types unless explicitly projected.

**Recommendation:** Never return full EF Core entity types from Bridge API endpoints. Always project to purpose-built `record` types via `.Select()`. This prevents over-fetching and accidental serialization of navigation properties (the leading cause of serialization cycles).

### E. Global Field Exclusion for Sensitive Data (Prisma `omit` pattern)
Prisma v6.2+ allows globally excluding fields like `password` from all queries.

**Recommendation:** Use EF Core query filters (`.HasQueryFilter()`) or explicit column projection in Bridge's repository layer to enforce sensitive field exclusion. Maintain a `PublicUserDto` that never includes credentials — and enforce that the full entity never reaches the API response serializer.

### F. Explicit Transaction Scope
Prisma's `$transaction(async (tx) => { ... })` makes transaction scope visible and explicit.

**Recommendation:** For multi-step operations in Bridge, always use `BeginTransactionAsync()` explicitly rather than relying on the implicit `SaveChanges()` transaction. Makes intent clear and avoids partial-save bugs.

### G. Config/Schema Separation (Prisma `prisma.config.ts` pattern)
Prisma v6 cleanly separates connection configuration (secrets) from schema definitions.

**Recommendation:** In Bridge, keep connection strings and runtime configuration strictly in `appsettings.json` / environment variables, never hardcoded in `DbContext`. Pass `DbContextOptions` via DI only. This is idiomatic .NET but worth making explicit in Bridge's architecture docs.

---

## Sharp Edges

| Issue | Severity | Detail |
|---|---|---|
| MongoDB no Prisma Migrate | Critical for Mongo users | Schema-push only; no migration history |
| Raw query type safety | High | `$queryRaw` returns `unknown`; no compile-time column validation |
| No cross-database joins | High | One datasource per schema; federation must be at app layer |
| N+1 still possible in code | Medium | Explicit loops can still produce N+1; Prisma prevents lazy-load N+1 only |
| Schema re-generation friction | Medium | `prisma generate` must run before `tsc` in CI — add as pre-build step |
| Interactive transactions slow under load | Medium | Hold a real DB connection open for the async callback duration |
| Prisma Accelerate is paid | High for edge | No OSS self-hosted equivalent; `engineType: "client"` is the alternative but still preview in v6 |
| Large schema file | Low | 50+ models in one file becomes unwieldy; multi-file schema is v6 preview feature |
| No domain logic on models | Architectural | Prisma generates plain data objects; domain methods must live in a separate layer (appropriate for Sunfish's architecture, but notable) |

---

## Node.js BFF Adoption Assessment

If Bridge adds a Node.js BFF layer:

| Criterion | Assessment |
|---|---|
| PostgreSQL | ✅ First-class |
| SQL Server | ✅ Production-ready |
| Type safety | ✅ Best-in-class for TypeScript |
| Edge runtime | ✅ Via `engineType: "client"` + driver adapters (v6, preview) |
| Migrations workflow | ✅ Clean SQL-file workflow |
| Real-time | ⚠️ Requires Prisma Pulse (paid) or external pub/sub |
| Self-hosted cache/pool | ❌ Accelerate is SaaS-only |
| Prisma 7 migration cost | Medium | CLI flag changes; scripts need updates |
