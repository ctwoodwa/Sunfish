# ICM Routing and Pipeline Variant Selection

Use this guide to classify incoming requests and select the appropriate pipeline variant.

## Request Classification

All requests to Sunfish fall into one of seven categories. Use the heuristics below to classify
and route to a pipeline variant.

### 1. sunfish-feature-change
**When to use:** New features, new blocks, enhancements, new adapters, or demos

**Entry heuristics:**
- "Add support for X" where X is a new capability
- "Create a new block" (form, task, schedule, asset)
- "Build a demo or example for X"
- "Extend existing block with new features"
- "Add a new adapter or integration"
- Request affects user-facing behavior

**Typical affected areas:**
- foundation, ui-core, ui-adapters-*, blocks-*, apps/kitchen-sink, apps/docs

**Stage emphasis:**
- Discovery and architecture are heavyweight (design contracts carefully)
- Package design is detailed (new types, new APIs)
- Implementation plan is comprehensive (staging, testing strategy)
- Review includes API quality and docs completeness
- Release emphasizes changelog clarity and example updates

---

### 2. sunfish-api-change
**When to use:** Breaking changes, public contract updates, adapter interface changes

**Entry heuristics:**
- "Change the API of X" (rename, reorder, remove, change types)
- "Make X breaking" (deprecation -> removal)
- "Update the contract that adapters must follow"
- "Reorganize types in foundation"
- Request implies impact on existing consumers or adapters

**Typical affected areas:**
- foundation, ui-core, ui-adapters-* (all must maintain parity)

**Stage emphasis:**
- Discovery must identify all reverse dependencies
- Architecture is mandatory (ADR required; migration path defined)
- Package design covers all affected packages in detail
- Implementation plan must coordinate across all adapters
- Review includes adapter parity check and deprecation timeline
- Release emphasizes migration guide and clear communication

---

### 3. sunfish-scaffolding
**When to use:** CLI changes, generator updates, template changes

**Entry heuristics:**
- "Update the scaffolding CLI"
- "Add a new template or generator"
- "Change how Sunfish apps are initialized"
- "Add or modify command in tooling/scaffolding-cli"

**Typical affected areas:**
- tooling/scaffolding-cli, possibly foundation and ui-core

**Stage emphasis:**
- Discovery includes impact on downstream app creation
- Architecture covers template design and plugin points
- Scaffolding stage is heavyweight (implementation happens here)
- Implementation plan includes template testing and rollout strategy
- Review includes integration tests and example app generation
- Release includes CLI versioning and migration notes

---

### 4. sunfish-docs-change
**When to use:** Docs site updates, API documentation, usage guides, examples

**Entry heuristics:**
- "Update the docs site"
- "Add API documentation for X"
- "Create a usage guide for X"
- "Update kitchen-sink demo"
- "Fix docs links or typos"
- Request is primarily documentation or example-focused

**Typical affected areas:**
- apps/docs, apps/kitchen-sink, any corresponding source files

**Stage emphasis:**
- Discovery is lightweight (scope is clear)
- Architecture may be skipped if purely docs
- Package design may reference existing APIs
- Implementation plan is straightforward
- Review includes rendering, link checks, and example correctness
- Release may be automated (docs-only trigger)

---

### 5. sunfish-quality-control
**When to use:** Review gates, audits, release readiness, consistency checks

**Entry heuristics:**
- "Audit API consistency across adapters"
- "Check naming consistency"
- "Verify compat-telerik compatibility"
- "Pre-release quality checklist"
- "Assess test coverage gaps"
- Request is about verification or gate-keeping, not new code

**Typical affected areas:**
- All packages, release readiness assessment

**Stage emphasis:**
- Intake and discovery are detailed (audit plan specified)
- Architecture may not apply (existing design review)
- Package design focuses on consistency check
- Implementation plan is the audit/check script
- Build stage may be skipped
- Review is the central stage (findings and recommendations)
- Release documents audit results and sign-off

---

### 6. sunfish-test-expansion
**When to use:** Test coverage, regression tests, parity matrices, scenario coverage

**Entry heuristics:**
- "Improve test coverage for X"
- "Add regression tests for the bug in X"
- "Ensure all adapters have the same test matrix"
- "Expand scenario coverage for X"
- Request is test-focused without changing implementation

**Typical affected areas:**
- Any package with insufficient test coverage, adapter parity testing

**Stage emphasis:**
- Discovery covers existing coverage and gaps
- Architecture may not apply (implementation strategy is test strategy)
- Package design defines test matrix and scenarios
- Scaffolding may include test generation tools
- Implementation plan details test rollout
- Build includes test development
- Review focuses on coverage metrics and test quality
- Release may include coverage badges or metrics

---

### 7. sunfish-gap-analysis
**When to use:** Finding and resolving missing capabilities, parity gaps, doc gaps

**Entry heuristics:**
- "Sunfish is missing X capability"
- "Feature X works in Blazor but not in React" (parity gap)
- "We don't have docs for X"
- "The tooling doesn't support X scenario"
- Request is exploratory or scoping a known gap

**Typical affected areas:**
- All packages, identified as missing or inconsistent

**Stage emphasis:**
- Discovery is heavyweight (gap scoping and impact assessment)
- Architecture covers design decisions to close the gap
- Package design details the new or modified APIs
- Implementation plan is phased (if multi-stage closure)
- Build includes implementation or docs/tooling additions
- Review assesses gap closure completeness
- Release communicates the new capability

---

## Quick Decision Tree

```
Is it a new feature, block, or demo?
  → sunfish-feature-change

Is it changing a public API or breaking compatibility?
  → sunfish-api-change

Is it CLI, templates, or generators?
  → sunfish-scaffolding

Is it docs, examples, or kitchen-sink?
  → sunfish-docs-change

Is it an audit, review gate, or consistency check?
  → sunfish-quality-control

Is it test coverage, regression, or parity testing?
  → sunfish-test-expansion

Is it finding or scoping a missing capability?
  → sunfish-gap-analysis
```

## Pipeline Variant Usage

Once you've classified the request:

1. **Read** `/icm/00_intake/CONTEXT.md` to create an intake note
2. **Reference** `/icm/pipelines/[variant]/README.md` for overview
3. **Follow** `/icm/pipelines/[variant]/routing.md` for stage navigation
4. **Check** `/icm/pipelines/[variant]/deliverables.md` for expected outputs at each stage
5. **Progress** through the default stages (00–08), using variant guidance

Pipeline variants do not create separate stage trees. They guide how you use the default stages.

## Examples

### Example 1: New Block
**Request:** "Create a new calendar block for scheduling workflows."

**Classification:** sunfish-feature-change
- New user-facing capability ✓
- Affects blocks-scheduling package ✓
- Needs kitchen-sink demo and docs ✓

**Route:** `/icm/pipelines/sunfish-feature-change/routing.md`
- Emphasize 02_architecture (contract with adapters)
- Emphasize 03_package-design (blocks-scheduling API)
- Require 06_build implementation in both React and Blazor
- Require kitchen-sink demo in 06_build
- Require docs in 08_release

---

### Example 2: Refactor Telerik Compatibility
**Request:** "Ensure compat-telerik stays compatible with the new form block APIs."

**Classification:** sunfish-quality-control
- Audit/gate-keeping work ✓
- Affects compatibility layer ✓
- No new implementation (yet) ✓

**Route:** `/icm/pipelines/sunfish-quality-control/routing.md`
- Lightweight 01_discovery (scope is clear)
- Possible 02_architecture (if gaps found)
- 03_package-design focuses on compat-telerik API mapping
- Build stage may involve adaptation, not new code
- 07_review is the key stage (findings, recommendations)
- Release documents compatibility sign-off

---

### Example 3: Test Coverage for React Adapter
**Request:** "We need higher test coverage for the React adapter; parity with Blazor."

**Classification:** sunfish-test-expansion
- Test-focused ✓
- Adapter parity concern ✓
- No API changes ✓

**Route:** `/icm/pipelines/sunfish-test-expansion/routing.md`
- 01_discovery: identify test gaps and coverage targets
- Skip or lightweight 02_architecture (existing design)
- 03_package-design: test matrix and scenarios
- 04_scaffolding: test generators if applicable
- 06_build: implement new tests
- 07_review: assess coverage metrics
- 08_release: publish coverage badge

---

## Choosing Entry Points

**Always start in 00_intake** unless you are reusing an existing output folder from a prior stage.

If you are **continuing or accelerating** an existing stage output (e.g., you have a discovery
document from a prior request and want to move straight to architecture), note this explicitly
in 00_intake and link to the prior work.
