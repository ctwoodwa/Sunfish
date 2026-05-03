<!--
Universal Planning Framework (UPF) — Sunfish integration
Source:   https://github.com/primeline-ai/universal-planning-framework
License:  MIT
Version:  1.2 (fetched 2026-04-20)
Adoption: see _shared/engineering/planning-framework.md for how UPF maps to
          Sunfish's ICM pipeline and ODF governance (GOVERNANCE.md).

This file is an unmodified copy of the UPF rule. When UPF releases a new
version, this file should be resynced from upstream. Any Sunfish-specific
adaptations live in _shared/engineering/planning-framework.md, not here.
-->

# Universal Planning Framework
**Version 1.2** | Planning, Validation, Meta-Cognition

---

## Core Purpose

This framework addresses a critical gap: major discoveries happen *during* execution, not during planning. The three-stage approach (Stage 0: Discovery, Stage 1: Planning, Stage 2: Meta-validation) ensures that assumptions are tested before implementation begins.

---

## Three-Stage Structure

### **Stage 0: Before Planning** (Discovery & Sparring)

Twelve contextual checks operationalize the DSV principle: "Decompose-Suspend-Validate." Not all checks apply to every plan—the framework is intelligence-driven.

**Priority tiers guide which checks to run:**
- Always consider (non-trivial plans): Existing Work, Feasibility, Better Alternatives
- Always for coding: Official Docs
- Usually relevant: Factual Verification, ROI analysis
- Context-dependent: Updates, Constraints, People Risk

The "AHA Effect" (Check 0.9) identifies fundamentally simpler approaches—the single highest-value discovery activity.

**Skip Stage 0** only when all apply: <3 phases, <2 hours effort, fully reversible changes, or explicit user override.

---

### **Stage 1: The Plan** (5 Core + 18 Conditional Sections)

**5 CORE sections (always required):**

1. **Context & Why** — Problem definition (max 3 sentences)
2. **Success Criteria** — Measurable outcomes + FAILED conditions (kill triggers + timeouts)
3. **Assumptions & Validation** — "Assumption → VALIDATE BY → IMPACT IF WRONG" format
4. **Phases** — Binary gates (PASS/FAIL only), scope-based for coding, observable deliverables
5. **Verification** — Automated (tests), Manual (reviews), Ongoing Observability (production monitoring)

**Key principle:** Phase sizing in coding domains uses scope (files touched, features delivered) rather than hours, since AI execution removes false precision from time estimates.

**18 CONDITIONAL sections** activate based on context: Rollback Strategy, Risk Assessment, Post-Completion Plan, Budget & Resources, User Validation, Legal & Compliance, Security & Privacy, Resume Protocol, Incremental Delivery, Delegation & Team Strategy, Dependencies & Blockers, Related Work & Integration, Timeline & Deadlines, Stakeholder Communication, Reference Library, Learning & Knowledge Capture, Feedback Architecture, Completion Gate.

---

### **Stage 1.5: Autonomous Hardening** (Optional)

Stress-test from six adversarial perspectives: Outside Observer, Pessimistic Risk Assessor, Pedantic Lawyer, Skeptical Implementer, The Manager, Devil's Advocate.

Output: Hardened plan + Hardening Log documenting findings and fixes.

---

### **Stage 2: Meta-Validation** (7 Checks)

1. Delegation strategy clarity
2. Research needs identification
3. Review gate placement
4. Anti-pattern scan (21 patterns)
5. Cold Start Test (can fresh agent execute this?)
6. Plan Hygiene Protocol
7. Discovery Consolidation Check

---

## 21 Anti-Patterns

**Core (1-12):** Unvalidated assumptions, vague phases, vague success criteria, no rollback, plan ending at deploy, missing Resume Protocol, delegation without contracts, blind delegation trust, skipping Stage 0, first idea remaining unchallenged, zombie projects (no kill criteria), timeline fantasy.

**AI-Specific (13-17):** Confidence without evidence, wrong detail distribution, premature precision, hallucinated effort estimates, delegation without context transfer.

**Quality (18-21):** Unverifiable gates, missing tool fallbacks, discovery amnesia, assumed facts without sources.

---

## Quality Rubric

- **C (Viable):** All 5 CORE + ≥1 CONDITIONAL. No critical anti-patterns.
- **B (Solid):** C + Stage 0 completed + FAILED conditions + Confidence Level + Cold Start Test.
- **A (Excellent):** B + sparring executed + Review Checkpoints + Reference Library (coding) + Knowledge Capture + Replanning triggers defined.

---

## Domain-Specific Activation

Framework automatically includes relevant sections for 8 domains: Software Development, Multi-Agent/AI Systems, Business/Strategy, Content/Marketing, Infrastructure/DevOps, Data & Analytics, Research/Exploration, Multi-Domain projects.

---

## Activate When

**Use for:** New features, architecture changes (3+ files), multi-phase projects (3+ phases or >2h), anything with external dependencies.

**Skip when ALL apply:** Single file, no dependencies, <50 lines, no migration, no user-facing change, rollback = git revert.
