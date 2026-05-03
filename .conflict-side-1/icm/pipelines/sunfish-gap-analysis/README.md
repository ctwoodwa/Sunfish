# sunfish-gap-analysis Pipeline

**Purpose:** Identify and resolve missing capabilities, adapter parity gaps, and documentation gaps.

## When to Use

Use this pipeline when the request involves:
- Finding missing features ("Sunfish doesn't support X")
- Adapter parity gaps (React has feature X but Blazor doesn't)
- Documentation gaps (feature exists but isn't documented)
- Scaffold/tooling gaps (missing generators or templates)
- Cross-adapter consistency issues

## Key Characteristics

- **Discovery-focused** (finding the gap is part of the work)
- **Stage 01 (Discovery) is heavyweight** — scoping the gap
- **Stage 02 (Architecture) is heavyweight** — designing the fix
- Leads to remediation plan (may span multiple releases)

## Typical Flow

1. **Intake:** "Form field validation works in Blazor but not React"
2. **Discovery:** Research why gap exists, impact assessment
3. **Architecture:** Design how to close the gap
4. **Implementation-plan:** Roadmap for remediation
5. **Build/Release:** Implement gap closure (or document approved gap)

## Outcome

- Gap analysis document (what's missing, why, impact)
- Remediation plan (how to close gap, timeline)
- Sign-off (gap closure prioritized or approved as not-to-fix)
