# Wave 1 Subagent Brief — Overview demo page authoring

**Purpose:** This document is the canonical instruction template for each parallel subagent
in the Wave 1 fan-out. Each subagent owns one family (buttons, layout, navigation, etc.) and
authors Overview demo pages for every sunfish-implemented component in that family.

---

## What you are doing

For every component in your assigned family that is marked `status: sunfish-implemented`
in `_shared/product/example-catalog.yaml`, create a new Overview demo page at:

```
apps/kitchen-sink/Pages/Components/<Family>/<Component>/Overview/Demo.razor
```

Where:
- `<Family>` is the catalog family slug capitalized (Buttons, Layout, Navigation, etc.)
- `<Component>` is the component name in the folder convention used by the catalog

Each demo page uses `SunfishExamplePanel` (see the canonical proof-point at
`apps/kitchen-sink/Pages/Components/Buttons/Button/Overview/Demo.razor`).

---

## Mandatory page shape

```razor
@page "/components/<family>/<component>/overview"
@layout MainLayout
@using Sunfish.UIAdapters.Blazor.Components.Showcase

<PageTitle><Component> / Overview · Sunfish Kitchen Sink</PageTitle>

<SunfishExamplePanel Title="<Component> / Overview"
                     Breadcrumb="@_breadcrumb"
                     GitHubUrl="https://github.com/ctwoodwa/Sunfish/blob/main/apps/kitchen-sink/Pages/Components/<Family>/<Component>/Overview/Demo.razor"
                     Sources="@GeneratedSources">

    <Narrative>
        <!-- 1–2 short paragraphs describing what the component does and how it is used.
             Reference concrete API surface (Variant, Size, OnClick, etc.) where helpful. -->
    </Narrative>

    <Example>
        <!-- A canonical example that demonstrates the component at a glance.
             Prefer one <div class="demo-row">…</div> per logical configuration group.
             Keep this focused: this is the OVERVIEW, not every feature. -->
    </Example>

</SunfishExamplePanel>

@code {
    private static readonly string[] _breadcrumb =
        ["Components", "<Family>", "<Component>", "Overview"];

    // Any minimal state needed by the Example (counters, models, etc.)
}
```

**Do NOT** hand-wire a `_sources` field. The MSBuild target
(`apps/kitchen-sink/build/SunfishDemoSources.targets`) auto-generates
`GeneratedSources` at build time from the files in your folder.

---

## Where to source the Example content

**Preferred:** Read the existing legacy page at
`apps/kitchen-sink/Pages/Components/<Component>/Overview.razor` and extract the **first
`<DemoSection>` block** under the `<PageSection Title="Overview">`. Adapt its markup to
sit inside the `<Example>` slot. The legacy page uses `ComponentDemoLayout`; your new page
uses `MainLayout` + `SunfishExamplePanel`.

**Fallback (no legacy page, or legacy has no Overview section):** Author a minimal
idiomatic usage example that exercises the component's primary API.

**Do NOT delete the legacy page.** It is still referenced by `ComponentRegistry` and
will be migrated in a separate wave. Parallel coexistence is by design.

---

## Narrative content

Write 1–2 short paragraphs that:
1. State what the component is and its primary role
2. List 2–4 canonical parameters or slots with backticked names (e.g., `Variant`, `OnClick`)
3. If applicable, link to sibling feature demos that don't exist yet
   (e.g., `<a href="/components/buttons/button/events">events</a>`) — these will 404
   gracefully until later waves land them

---

## Constraints

- Do **not** modify any component in `packages/ui-adapters-blazor/`. This wave is
  documentation-only.
- Do **not** touch `ComponentRegistry.cs`. Legacy nav continues to work.
- Do **not** modify the catalog YAML. Rely on the validated copy.
- Do **not** rename or migrate the legacy `<Component>/Overview.razor` file.
- Every new `Demo.razor` must compile. If a component's API is unclear, add a TODO
  comment in the narrative and move on — don't block on API spelunking.

---

## Validation

Before declaring your family complete:

1. Build: `dotnet build apps/kitchen-sink/Sunfish.KitchenSink.csproj`. Must succeed.
2. Confirm each new Demo.razor has a `Sources.g.cs` produced in
   `apps/kitchen-sink/obj/Debug/net11.0/SunfishDemoSources/`. File name is the URL
   path joined by underscores.
3. Navigate (mentally) the routes — list them in your report so the next wave can
   verify.

---

## Report format

Report back a brief, under 200 words:
- Family you handled
- Count of components processed
- Count of Demo.razor files created
- Any components that were in the catalog but had no tree implementation
  (catalog drift — flag these)
- Any blockers or ambiguities you escalated as TODOs
- Build result: PASS / FAIL

Do **not** paste file content in the report — the files themselves are the deliverable.
