---
uid: foundation-integrations-credentials
title: Integrations — Credentials
description: The CredentialsReference shape, why it never holds plaintext secrets, and how rotation and expiry are tracked.
---

# Integrations — Credentials

## The contract: no secrets on the surface

`CredentialsReference` is a deliberately boring record. It names **where** the secret can be resolved — never **what** the secret is.

```csharp
public sealed record CredentialsReference
{
    public required string ProviderKey { get; init; }
    public required string Scheme { get; init; }           // apiKey, oauth2, mtls, ...
    public required string ReferenceId { get; init; }      // vault path / secret-manager key
    public DateTimeOffset? RotatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
```

The rule is strict: **plaintext secrets never cross a contract in `Sunfish.Foundation.Integrations`**. The reference points a host-specific secrets-management adapter at the secret's location (an Azure Key Vault path, an AWS Secrets Manager key, a file on disk in a local-first deployment); resolution happens in that adapter and returns the material only to code that handles outbound transport.

## Scheme values

`Scheme` is a free-form short identifier that the adapter interprets:

- `apiKey` — a single long-lived token.
- `oauth2` — an OAuth client credential pair or refresh-token bundle.
- `mtls` — a mutual-TLS certificate pair.
- `hmac` — a shared-secret HMAC signing key.
- `none` — no credential (rare; some public feeds require nothing).

Each adapter documents which schemes it accepts; a taxonomy ADR is not required until cross-adapter interop demands one.

## Scope

The `ProviderKey` field scopes a reference to one registered provider. Credentials for the same provider may exist per tenant, per environment, or globally — the host decides. A typical tenant-scoped shape pairs `CredentialsReference` with a persisted record that also carries a `TenantId` and the reference metadata. This Foundation package does not dictate that shape so different hosts (Bridge, lite-mode, self-hosted) can store the surrounding record wherever makes sense for them.

## Rotation and expiry

`RotatedAt` and `ExpiresAt` are optional timestamps tracked on the reference itself, even though the underlying secret lives elsewhere:

- `RotatedAt` — the last time the secret backing this reference was rotated. Admins use this to prove a rotation policy is being followed.
- `ExpiresAt` — when the credential is expected to become invalid. Admin surfaces surface an expiry warning ahead of the date so operators can rotate before outages.

Tracking them on the reference — rather than only inside the secrets-manager — keeps rotation visibility on the same surface that lists providers. A scheduled job can list all references whose `ExpiresAt` falls inside a warning window without pulling secrets.

## Resolving credentials

Resolution is a host-level concern, but every adapter uses the same shape. A host provides an `ICredentialsResolver` (lives in a host-specific secrets package, not in `Sunfish.Foundation.Integrations`) that takes a `CredentialsReference` and returns a transport-ready secret for the configured scheme:

```csharp
// Illustrative — this contract is host-specific, not in foundation-integrations.
public interface ICredentialsResolver
{
    ValueTask<ResolvedCredential> ResolveAsync(CredentialsReference reference, CancellationToken ct);
}

public sealed class StripeGateway
{
    private readonly ICredentialsResolver _credentials;
    public StripeGateway(ICredentialsResolver credentials) => _credentials = credentials;

    public async ValueTask ChargeAsync(CredentialsReference reference, Money amount, CancellationToken ct)
    {
        var resolved = await _credentials.ResolveAsync(reference, ct);
        // use resolved.SecretMaterial in the outbound call; never log it
    }
}
```

The foundation contract ends at the reference record; plaintext handling is always inside an adapter that is responsible for its own audit and logging policy.

## Audit and least privilege

Because `CredentialsReference` is a plain record with no secret material, it is safe to log, audit, and include in diagnostics. Stripe rotation reports, "which tenants reference this expiring key?" queries, and admin UI listings all work against the reference without risking exposure. Plaintext secrets stay inside the resolver / transport boundary, where narrower audit policies apply.

## Related

- [Overview](overview.md)
- [Provider Registry](registry.md)
- [Webhooks](webhooks.md)
