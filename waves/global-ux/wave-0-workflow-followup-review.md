# Wave 0 Review — blocks-workflow DI follow-up

**Date:** 2026-04-25
**Code commit:** a201f3d8
**Report commit:** 1c4dedf8

## Per-criterion results

**(a) PASS** — `Sunfish.Blocks.Workflow.csproj` diff adds exactly one new `<ItemGroup>` containing only `<ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />`. No PackageReference, no other content. (See `git show a201f3d8` — 3 insertions in csproj.)

**(b) PASS** — `WorkflowServiceCollectionExtensions.cs` diff adds exactly:
- `using Microsoft.Extensions.DependencyInjection.Extensions;` (line 2, alphabetical position immediately after `Microsoft.Extensions.DependencyInjection`)
- `using Sunfish.Foundation.Localization;` (line 3, alphabetical)
- One body line: `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));` inside the existing `AddInMemoryWorkflow` method, immediately after the runtime registration.

No other behavioral changes — the `AddSingleton<IWorkflowRuntime, ...>` line and `return services;` are untouched.

**(c) PASS** — Uses `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));` (idempotent open-generic registration). Functionally equivalent to the canonical Pattern A in `blocks-accounting/DependencyInjection/AccountingServiceCollectionExtensions.cs` line 31, which uses `services.TryAdd(ServiceDescriptor.Singleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>)))`. Both call into `ServiceCollectionDescriptorExtensions.TryAdd` and both register a Singleton open-generic — `TryAddSingleton(Type, Type)` is the documented sugar form. Pattern parity confirmed; consider this a permitted minor stylistic variant rather than a deviation. Optionally, future audit could normalize on the `TryAdd(ServiceDescriptor...)` form for verbatim canonical match.

**(d) PASS** — Re-ran `dotnet build packages/blocks-workflow/Sunfish.Blocks.Workflow.csproj`: "Build succeeded. 0 Warning(s) 0 Error(s)". `Sunfish.Analyzers.LocComments` loaded (visible in build output) and emitted no `SUNFISH_I18N_001` warnings. Only NETSDK1057 informational messages from .NET 11 preview SDK, unrelated to this change. Matches the report's build excerpt exactly.

**(e) PASS** — Commit message subject: `feat(i18n): wave-0-workflow-followup — blocks-workflow DI completion`. Body also contains explicit `Token: wave-0-workflow-followup` line.

**(f) PASS** — `git show --name-only a201f3d8` returns exactly:
- `packages/blocks-workflow/Sunfish.Blocks.Workflow.csproj`
- `packages/blocks-workflow/src/WorkflowServiceCollectionExtensions.cs`

Two files, both in scope. No accelerator/ICM/docs/.wolf bleed.

**(g) PASS** — Verified by reading `git show a201f3d8:packages/blocks-workflow/src/WorkflowServiceCollectionExtensions.cs`. The XML doc on `AddInMemoryWorkflow` references only `<see cref="InMemoryWorkflowRuntime"/>` and `<see cref="IWorkflowRuntime"/>` — both workflow-internal types resolvable without `Microsoft.Extensions.Localization`. There is no `<c>IStringLocalizer&lt;T&gt;</c>` plain-text fallback in the file (pre-change file confirms same). Subagent's skip is correct — instruction was conditional ("if … restore it") and the precondition is false. Avoided introducing an unused `using Microsoft.Extensions.Localization;`. Minor nit: the commit message says "plus restoration of XML doc-cref" which slightly overstates what happened (no cref was actually restored), but this is a wording artifact, not a correctness issue.

## Final verdict: GREEN
