# NOTICE — Sunfish.Blocks.FinancialTaxBridge

## License

MIT. See repository root `LICENSE` for the full text.

## Origin

Clean-room original. The adapter pattern is industry-standard (GoF); no third-party source was consulted in the design or implementation of this package. The bridge ships under MIT as a routine Sunfish substrate addition.

## Third-party dependencies

This package depends only on other Sunfish first-party packages:

- `Sunfish.Blocks.FinancialTax`
- `Sunfish.Blocks.FinancialAr`
- `Sunfish.Blocks.FinancialAp`
- `Microsoft.Extensions.Options` (Microsoft, MIT)
- `Microsoft.Extensions.DependencyInjection.Abstractions` (Microsoft, MIT, transitively via the bridge's `IServiceCollection` extension method)

No FOSS source code (BSL, AGPL, GPL, MPL, etc.) was studied or referenced.
