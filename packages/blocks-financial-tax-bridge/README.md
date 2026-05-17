# Sunfish.Blocks.FinancialTaxBridge

Bridge package that wires the canonical `Sunfish.Blocks.FinancialTax` engine into AR's and AP's local `ITaxCalculator` interfaces.

## What this package is

- **Pure adapter.** Two adapters (`ArTaxCalculatorAdapter`, `ApTaxCalculatorAdapter`) that both wrap the canonical `Sunfish.Blocks.FinancialTax.Services.ITaxCalculationService`.
- A DI extension (`AddBlocksFinancialTaxBridge()`) that replaces AR's + AP's `NoOpTaxCalculator` registrations with the real adapters.
- A small options class (`BlocksFinancialTaxBridgeOptions`) carrying a default `TaxLocationContext` used for every adapter call.

## What this package is NOT

- Not a new tax engine — calculation logic lives in `blocks-financial-tax`.
- Not a replacement for `NoOpTaxCalculator` in AR/AP packages — those stay as the default so AR/AP can ship without a transitive dependency on the tax cluster.
- Not opinionated about which jurisdiction model to apply — that's still inside `blocks-financial-tax`.

## When to use it

When a host wants AR/AP invoice posting to compute real tax instead of returning zero. Without this bridge, AR/AP wire `NoOpTaxCalculator` and skip tax computation entirely.

## DI wiring

**Call AFTER** `AddBlocksFinancialAr()`, `AddBlocksFinancialAp()`, and `AddBlocksFinancialTax()`. The bridge `Replace`s AR's + AP's existing `ITaxCalculator` registrations, so those calls must run first.

```csharp
using Sunfish.Blocks.FinancialAr.DependencyInjection;
using Sunfish.Blocks.FinancialAp.DependencyInjection;
using Sunfish.Blocks.FinancialTax.DependencyInjection;
using Sunfish.Blocks.FinancialTaxBridge.DependencyInjection;
using Sunfish.Blocks.FinancialTax.Models;

services.AddBlocksFinancialAr();
services.AddBlocksFinancialAp();
services.AddBlocksFinancialTax();

// Optional — set a non-default jurisdiction.
services.Configure<BlocksFinancialTaxBridgeOptions>(o =>
    o.DefaultLocation = new TaxLocationContext(IsoCountry: "US", Region: "US-VA"));

services.AddBlocksFinancialTaxBridge();
```

## Why a `BlocksFinancialTaxBridgeOptions` shim

AR's + AP's local `ITaxCalculator` surface is `(taxCodeId, taxableBase, transactionDate) → decimal`. It does NOT carry a per-line `TaxLocationContext`. The canonical `ITaxCalculationService` requires one.

The bridge closes the gap with a per-host default location. Hosts operating in a single jurisdiction set it once at boot; hosts with per-call jurisdiction needs register their own `ITaxCalculator` decorator instead of this bridge.

## License

MIT (see `NOTICE.md`).
