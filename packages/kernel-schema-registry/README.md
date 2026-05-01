# Sunfish.Kernel.SchemaRegistry

Sunfish kernel §3.4 Schema Registry — JSON Schema draft 2020-12 via `JsonSchema.Net`, content-addressed via `IBlobStore`, in-memory default backend.

**Validation path shipped.** Migration path (jsonata-style transforms) is a follow-up.

## What this ships

### Contracts

- **`ISchemaRegistry`** — read/write registry: `RegisterAsync(name, version, schemaJson)` + `LookupAsync(name, version)` + `ValidateAsync(name, version, payload)`.
- **`SchemaIdentity`** — content-addressed identifier (`Vendor.Domain.Name@Version` + content hash).
- **`SchemaValidationError`** — typed error with JSON-pointer pointing at the offending field.

### Reference impl

- **`InMemorySchemaRegistry`** — Dictionary-backed reference; backs schemas via `IBlobStore` for content-addressed storage.
- **`JsonSchemaValidator`** — `JsonSchema.Net` adapter for draft 2020-12 validation.

### Validation flow

```
payload + (schema-name, schema-version)
  → registry.LookupAsync → JsonSchema instance
    → validator.Validate(payload) → ValidationResult
      → Success | List<SchemaValidationError>
```

## Migration (deferred)

The follow-up scope adds a `jsonata`-style migration path: schemas can declare "from version N-1 to version N, apply transform T". The registry composes transforms across versions so callers can up-convert legacy payloads at read time without explicit migration code.

## DI

```csharp
services.AddSunfishSchemaRegistry();
```

## ADR map

- Sunfish kernel §3.4 (Schema Registry)
- ADR 0055 (dynamic-forms substrate; schema-registry consumer)

## See also

- `Sunfish.Foundation.Taxonomy` — adjacent versioned-reference-data substrate (the "registry" pattern shows up in both)
