# Sunfish.Analyzers.ProviderNeutrality

Roslyn analyzer that enforces [ADR 0013](../../../docs/adrs/0013-foundation-integrations.md)
provider-neutrality at build time.

## Rules

### SUNFISH_PROVNEUT_001 (Error)

Vendor SDK namespace referenced from a non-providers package.

Domain code in `packages/blocks-*` and `packages/foundation-*` must not reference
vendor SDK namespaces (`Stripe.*`, `Plaid.*`, `SendGrid.*`, `Twilio.*`). Only
`packages/providers-*` packages may take vendor-SDK dependencies. The contract
seam — `Sunfish.Foundation.Integrations` — is excluded from the rule because
it defines the vendor-neutral interfaces that providers implement.

**Why a mechanical gate?** ADR 0013 declares vendor-neutrality load-bearing.
Without a build-time check the policy is socially enforced ("reviewers reject
violations"); the moment a developer slips, vendor references multiply across
N callers. This analyzer fails the build instead.

## Auto-attach

`Directory.Build.props` auto-wires this analyzer onto every project under
`packages/blocks-*/` and `packages/foundation-*/` (excluding the
`Sunfish.Foundation.Integrations` contract package + test projects).
Mirrors the `loc-comments` / `loc-unused` analyzer auto-wiring pattern.
