---
uid: block-financial-tax-bridge-overview
title: blocks-financial-tax-bridge
---

# `blocks-financial-tax-bridge`

Bridge package that wires the canonical `Sunfish.Blocks.FinancialTax` engine into AR's and AP's local `ITaxCalculator` interfaces. Pure adapter — no calculation logic of its own.

## What it is

- Two adapters (`ArTaxCalculatorAdapter`, `ApTaxCalculatorAdapter`) implementing AR's and AP's local `ITaxCalculator`, delegating to the canonical `Sunfish.Blocks.FinancialTax.Services.ITaxCalculationService`.
- A DI extension (`AddBlocksFinancialTaxBridge()`) that `Replace`s AR's + AP's `NoOpTaxCalculator` registrations with the real adapters.
- A small options class (`BlocksFinancialTaxBridgeOptions`) carrying a default `TaxLocationContext` used for every adapter call.

## Why it exists

AR's + AP's local `ITaxCalculator` is intentionally decoupled from `blocks-financial-tax`'s richer surface — AR/AP ship without a transitive dependency on the tax cluster, and `NoOpTaxCalculator` is the default. Hosts that want real tax computation register this bridge.

## When to use it

When a host wants AR/AP invoice posting to compute real tax instead of returning zero. Without this bridge, AR/AP wire `NoOpTaxCalculator` and skip tax computation entirely.

## DI wiring

Register **AFTER** the AR / AP / Tax registrations — the bridge `Replace`s prior `ITaxCalculator` registrations and needs them present:

```csharp
services.AddBlocksFinancialAr();
services.AddBlocksFinancialAp();
services.AddBlocksFinancialTax();

services.Configure<BlocksFinancialTaxBridgeOptions>(o =>
    o.DefaultLocation = new TaxLocationContext(IsoCountry: "US", Region: "US-VA"));

services.AddBlocksFinancialTaxBridge();
```

## Default location

`BlocksFinancialTaxBridgeOptions.DefaultLocation` defaults to `new TaxLocationContext(IsoCountry: "US")`. AR's + AP's local `ITaxCalculator` does not carry a per-line `TaxLocationContext`, so the bridge forwards the configured default into every canonical call. Hosts with per-call jurisdiction needs register their own decorator instead.

## Error handling

When the canonical engine returns a non-`TaxCalculationError.None` result, the adapter returns `0m` (per AR's and AP's local interface contract — failure is a zero-tax result, not an exception).

## Tests

14 unit tests (7 per adapter) cover: null/whitespace tax-code-id → 0, canonical success → forwarded tax amount, canonical error → 0, transaction-date forwarding, cancellation-token forwarding, and configured-location forwarding.

## Related

- `Sunfish.Blocks.FinancialAr.Services.ITaxCalculator` — AR's local surface
- `Sunfish.Blocks.FinancialAp.Services.ITaxCalculator` — AP's local surface
- `Sunfish.Blocks.FinancialTax.Services.ITaxCalculationService` — canonical engine
