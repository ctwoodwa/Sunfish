## Summary

<!-- What does this PR do? One or two sentences. -->

## ICM Stage

<!-- Which pipeline stage does this work come from? -->
- [ ] This PR has a corresponding ICM stage output in `/icm/`
- [ ] Accelerated / no ICM stage (explain below)

ICM stage: `<!-- e.g. 06_build — feat/my-feature -->`

## Affected Packages

<!-- Check all that apply -->
- [ ] `packages/foundation`
- [ ] `packages/ui-core`
- [ ] `packages/ui-adapters-blazor`
- [ ] `packages/ui-adapters-react`
- [ ] `packages/compat-telerik`
- [ ] `packages/blocks-*`
- [ ] `apps/`
- [ ] `tooling/`
- [ ] `accelerators/`
- [ ] Repo infrastructure / CI / docs only

## Checklist

- [ ] Build passes (`dotnet build`)
- [ ] Tests pass (`dotnet test`)
- [ ] No Blazor/framework types in `packages/foundation`
- [ ] Public API changes are XML-documented
- [ ] User-facing changes include kitchen-sink demo update
- [ ] User-facing changes include docs update
- [ ] `compat-telerik` impact considered (if applicable)
- [ ] Adapter parity maintained (Blazor + React, if applicable)
