# Layout family spec audit — 2026-04-21

Tier 1 spec-verification (ADR 0022) against every entry in
`apps/docs/component-specs/component-mapping.json` with
`status in ["implemented", "partial"]` whose spec falls under the Layout domain as named
in the task brief (appbar, card, carousel, dialog, drawer, gridlayout, panelbar, splitter,
stepper, tabstrip, wizard) plus the remaining layout-category entries (`stacklayout`,
`window`).

> Category reality-check: `carousel` is tagged `media` in the mapping; `stepper` and
> `wizard` are tagged `navigation`. Task brief named them explicitly, so they are
> audited here (the siblings `breadcrumb/contextmenu/menu/pager/toolbar` are owned by
> the Nav/Media squad). `wizard` is mapped as `planned` with `sunfish: null`, yet the
> Blazor impl exists and is wired to a demo, so it is included as "effectively
> implemented — mapping out of date". `dockmanager` and `tilelayout` (both planned) are
> skipped as instructed.

Severity values: `missing`, `bug`, `incomplete`, `covered`.
Verdict values: `verified` / `needs-work` / `downgrade-to-partial`.

---

## Summary

| Component              | Spec dir      | Mapping status | Verdict                  | Gap count (bug / missing / incomplete / covered) |
|------------------------|---------------|----------------|--------------------------|--------------------------------------------------|
| SunfishAppBar          | appbar        | implemented    | downgrade-to-partial     | 0 / 5 / 0 / 2 |
| SunfishCard (+slots)   | card          | implemented    | needs-work               | 0 / 5 / 1 / 4 |
| SunfishCarousel        | carousel      | implemented    | downgrade-to-partial     | 2 / 5 / 1 / 2 |
| SunfishDialog          | dialog        | implemented    | needs-work               | 0 / 4 / 1 / 5 |
| SunfishDrawer          | drawer        | implemented    | needs-work               | 0 / 4 / 1 / 5 |
| SunfishGridLayout      | gridlayout    | implemented    | needs-work               | 0 / 3 / 2 / 3 |
| SunfishAccordion       | panelbar      | implemented    | downgrade-to-partial     | 0 / 6 / 1 / 2 |
| SunfishSplitter        | splitter      | implemented    | needs-work               | 1 / 3 / 1 / 5 |
| SunfishStack           | stacklayout   | implemented    | needs-work               | 1 / 1 / 1 / 3 |
| SunfishStepper         | stepper       | implemented    | needs-work               | 1 / 3 / 1 / 4 |
| SunfishTabStrip        | tabstrip      | implemented    | needs-work               | 0 / 2 / 2 / 7 |
| SunfishWizard          | wizard        | planned\*      | needs-work               | 0 / 3 / 2 / 5 |
| SunfishWindow          | window        | partial        | needs-work (stays partial) | 0 / 3 / 1 / 7 |

\* Mapping says `sunfish: null, status: planned` but `SunfishWizard` is fully wired and
has a demo. The mapping is stale — see "Top-level next actions".

Totals: 13 audited, 0 `verified`, 10 `needs-work`, 3 `downgrade-to-partial`.
Aggregate gaps: **4 bug**, **47 missing**, **13 incomplete**, **48 covered**.

---

## Component: SunfishAppBar (spec: `appbar`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\SunfishAppBar.razor`
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Layout\AppBar\Overview\Demo.razor`
  *(no Appearance / Events / Accessibility demo pages — missing demo coverage)*
- **Spec files reviewed**: `overview.md`, `appearance.md`, `position.md`, `sections.md`,
  `separators.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<AppBarSection>` child component (overview.md + sections.md) | missing | Spec models AppBar content exclusively through `<AppBarSection>` tags with `Class`/`Visible`. Impl accepts raw `ChildContent` only. | Add `AppBarSection`, `AppBarSeparator`, `AppBarSpacer` sub-components. Keep raw `ChildContent` for fallback. |
  | `<AppBarSeparator>` / `<AppBarSpacer>` (separators.md) | missing | No dedicated components; users have to write `<span class="…">` by hand. | See above — same fix. |
  | `ThemeColor` parameter (appearance.md) | missing | Spec lists a `ThemeColor` string tied to `ThemeConstants.AppBar.ThemeColor` (Base/Primary/…). Impl has no equivalent. | Add `ThemeColor` string parameter; emit `mar-appbar--<color>` class. |
  | `Refresh()` reference method (overview.md) | missing | Spec documents a `@ref`-callable `Refresh()` that calls `StateHasChanged`. Impl has no such public method. | Add `public void Refresh() => StateHasChanged();`. |
  | Events demo / Accessibility demo pages (ADR 0022) | missing | Only `Overview/Demo.razor` exists. Other Layout families have four demo variants. | Add `Appearance/`, `Events/`, `Accessibility/` demo folders (events can be empty-but-present if the component has no events). |
  | `Position` parameter | covered | Impl maps spec `AppBarPosition` (Top/Bottom/None). | — |
  | `role="banner"` (accessibility) | covered | Impl renders `<header role="banner">`. | — |

- **Verdict**: `downgrade-to-partial` — the surface is too thin (no section/spacer/separator
  children, no ThemeColor, no Refresh); spec examples don't compile against impl.

---

## Component: SunfishCard (spec: `card`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\Card\` (SunfishCard.razor + SunfishCardActions / SunfishCardBody / SunfishCardFooter / SunfishCardHeader / SunfishCardImage).
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Card\Overview\Demo.razor`
  - `…\Card\Appearance\Demo.razor`
  - `…\Card\Accessibility\Demo.razor`
  *(no Events demo)*
- **Spec files reviewed**: `overview.md`, `slots.md`, `actions.md`, `appearance.md`,
  `orientation.md`, `separators.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<CardSeparator>` child (separators.md) | missing | Spec defines a `<CardSeparator>` (with `Orientation`) for splitting sections inside a card. Impl has none. | Add `SunfishCardSeparator` with `Orientation` parameter. |
  | `<CardTitle>` / `<CardSubTitle>` children (slots.md) | missing | Spec expects dedicated title/subtitle slots (distinct from `<CardHeader>`). Impl lacks both; users fall back to raw markup. | Add `SunfishCardTitle`, `SunfishCardSubTitle` components. |
  | `ThemeColor` (appearance.md) | missing | Spec lists a `ThemeColor` on card root. Impl has none. | Add `ThemeColor` string parameter; thread to `mar-card--<color>`. |
  | `Orientation` type (orientation.md) | incomplete | Spec: `Orientation` is `CardOrientation` enum (`Horizontal`/`Vertical`). Impl: `Orientation` is `string`. Loses type safety and autocompletion. | Introduce a `CardOrientation` enum in Foundation; migrate impl. |
  | `<CardActions>` `Orientation` parameter (actions.md) | missing | Spec supports horizontal/vertical actions. Impl only supports `ActionsLayout` (Start/Center/End) — no orientation switch. | Add `Orientation` parameter on `SunfishCardActions`. |
  | `Width` / `Height` / default size pattern | missing | Spec card overview shows `Width`/`Height` parameters first-class. Impl relies on `AdditionalAttributes`/class only. | Add explicit `Width` / `Height` parameters. |
  | Events demo page (ADR 0022) | incomplete | No `Events` demo (card has no dedicated events, but ADR 0022 asks for all four demo shells). | Add a placeholder `Events/Demo.razor`. |
  | Slot structure (`Header`/`Body`/`Footer`/`Image`) | covered | All four exist as first-class child components. | — |
  | `ActionsLayout` (actions.md "Default alignment = End") | covered | Impl default is `End`. | — |
  | Accessibility (article role) | covered | Impl uses `role="article"`. | — |
  | Image alt text (accessibility) | covered | `SunfishCardImage.Alt` is required and threaded to `<img alt>`. | — |

- **Verdict**: `needs-work` — core composition works; child-slot surface (Title/SubTitle/Separator)
  and theming are the meaningful gaps.

---

## Component: SunfishCarousel (spec: `carousel`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\SunfishCarousel.razor`
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Carousel\Overview\Demo.razor` (no Appearance / Events / Accessibility)
- **Spec files reviewed**: `overview.md`, `navigation.md`, `automatic-cycle.md`, `templates.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Page` / `PageChanged` (overview.md) | bug | Spec: selected slide is `Page` (**1-based int**) with `PageChanged`. Impl: `ActiveIndex` / `ActiveIndexChanged` (**0-based**). `@bind-Page="1"` from spec doc code does not compile. | Add `Page`/`PageChanged` 1-based aliases; internal arithmetic stays 0-based. Deprecate `ActiveIndex` but keep for back-compat. |
  | `AutomaticPageChange` / `AutomaticPageChangeInterval` (automatic-cycle.md) | bug | Spec names are `AutomaticPageChange` (bool) and `AutomaticPageChangeInterval` (int ms). Impl names are `AutoPlay` and `IntervalMs`. Spec code samples don't compile. | Add spec-named parameters; `AutoPlay`/`IntervalMs` become thin aliases. |
  | `<Template>` nested tag for data-driven slides (templates.md) | missing | Spec uses `<SunfishCarousel Data="…"><Template>@context…</Template>`. Impl uses a `RenderFragment<object>? ItemTemplate` parameter. Spec markup doesn't bind. | Add a `Template` render fragment child (i.e. a typed `RenderFragment<object>` exposed via `<Template>` child tag convention). |
  | `Rebind()` reference method (overview.md Reference) | missing | Spec documents `@ref`-callable `Rebind()` to force re-projection of `Data`. Impl has no equivalent. | Add `public void Rebind() => StateHasChanged();`. |
  | `ThemeColor` parameter (appearance/overview.md) | missing | Spec lists `ThemeColor` (default "light"). Impl none. | Add `ThemeColor` string parameter. |
  | `Arrows` parameter name (navigation.md) | incomplete | Spec parameter is `Arrows`. Impl names it `ShowArrows`. | Add `Arrows` alias; keep `ShowArrows` for compat. |
  | `Class` / `Width` / `Height` | covered | All three present. | — |
  | `LoopPages` / `Pageable` | missing | Spec parameter names match (`Pageable`, `LoopPages`), but **Pageable** on impl has no effect on dot render — it is ignored; dots always render. | Gate the `sf-carousel__dots` block on `Pageable`. |
  | Keyboard Arrow navigation (accessibility) | missing | Spec requires Arrow-key handling. Impl has no `@onkeydown` on the root. | Add keyboard handler calling Previous/Next on Arrow keys. |
  | Demo pages Appearance/Events/Accessibility | missing | Only Overview demo exists. | Add three demo folders. |

- **Verdict**: `downgrade-to-partial` — two bugs (breaks spec-sample compile) plus core
  missing surface (`Template`, `Rebind`, `ThemeColor`) and broken `Pageable` flag.

---

## Component: SunfishDialog (spec: `dialog`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Dialog\SunfishDialog.razor` (+ `SunfishConfirmDialog.razor`).
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Feedback\Dialog\{Overview,Appearance,Events,Accessibility}\Demo.razor`.
- **Spec files reviewed**: `overview.md`, `action-buttons.md`, `title.md`, `modal.md`,
  `focus.md`, `predefined-dialogs.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<DialogTitle>` nested tag (title.md) | missing | Spec: dialog title is a `<DialogTitle>` child RenderFragment (supports arbitrary HTML, not just string). Impl uses a `Title` string plus optional `TitleTemplate` — but no `<DialogTitle>` child component exists. | Add `SunfishDialogTitle` child component that registers a fragment on parent. |
  | `DialogButtonsLayout` enum (action-buttons.md) | missing | Spec: `ButtonsLayout` enum on `<DialogButtons>` (Start/Center/End/Stretch/Normal). Impl: `DialogActions` wrapper with no layout parameter. | Add `DialogButtonsLayout` enum and `ButtonsLayout` parameter on `<DialogActions>` (plus rename to `DialogButtons` for spec parity or expose both tag names). |
  | `ThemeColor` (overview.md) | missing | Spec lists `ThemeColor`. Impl none. | Add `ThemeColor` string parameter on root. |
  | `FocusedElementSelector` (focus.md) | missing | Spec: CSS-selector parameter to move initial focus into a specific inner element on open. Impl uses a hardcoded focus-first-button policy. | Add `FocusedElementSelector` parameter; fall back to current behaviour. |
  | `DialogFactory` service for `Alert`/`Confirm`/`Prompt` (predefined-dialogs.md) | incomplete | Spec registers `DialogFactory` in DI and exposes `Dialog.AlertAsync`, `Dialog.ConfirmAsync`, `Dialog.PromptAsync`. Impl has a `SunfishConfirmDialog` component but no factory service, no Alert, no Prompt. | Add `IDialogFactory` service registered in adapter DI; expose predefined dialogs. |
  | `<DialogActions>` tag name (action-buttons.md) | covered | Spec tag is `<DialogButtons>`; impl ships `<DialogActions>` — spec samples don't compile. Flagged under `DialogButtonsLayout` row but worth noting separately. | Ship `DialogButtons` as the canonical tag name; keep `DialogActions` as alias. |
  | `Modal` parameter | covered | Impl default `true`; matches spec. | — |
  | `CloseOnOverlayClick` | covered | Present. | — |
  | `Draggable` | covered | Present. | — |
  | `Visible` two-way binding | covered | Present. | — |
  | `aria-modal` / `role="dialog"` | covered | Emitted. | — |

- **Verdict**: `needs-work` — core feature set is healthy; missing child-tag parity
  (`DialogTitle`, spec-named `DialogButtons`) and the predefined-dialogs DI service are
  the headline gaps.

---

## Component: SunfishDrawer (spec: `drawer`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\SunfishDrawer.razor`
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Layout\Drawer\{Overview,Appearance,Events,Accessibility}\Demo.razor`.
- **Spec files reviewed**: `overview.md`, `position.md`, `data-binding.md`, `modes.md`,
  `mini-mode.md`, `templates.md`, `events.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Position` enum values (position.md) | incomplete | Spec enumerates `Start` / `End` (logical, LTR-aware). Impl enumerates `Left` / `Right` (physical). Spec snippets using `DrawerPosition.Start` don't compile. | Add `Start`/`End` members to enum (map to Left/Right for LTR); or rename and keep Left/Right as aliases. |
  | `<Template>` whole-drawer render fragment (templates.md) | missing | Spec: a `<Template>` tag replaces the entire drawer body for total-custom rendering. Impl only offers per-item `ItemTemplate`. | Add a `DrawerTemplate` (or `<Template>`) RenderFragment parameter rendered instead of the default item list. |
  | `SeparatorField` (data-binding.md) | missing | Spec supports a `SeparatorField` boolean-property mapping on the data model. Impl lacks it. | Add `SeparatorField` parameter and render `<hr>` for items where the mapped field is true. |
  | `OnItemRender` event + `DrawerItemRenderEventArgs` (events.md) | missing | Spec fires a per-item render event with event args exposing `Item` and `Class`. Impl has no such event. | Add `OnItemRender` `EventCallback<DrawerItemRenderEventArgs>` and wire into the item loop. |
  | `@ref` programmatic `ExpandAsync`/`CollapseAsync`/`ToggleAsync` | covered | All three exist. | — |
  | `MiniMode` + `MiniWidth` | covered | Present. | — |
  | `Mode` (Overlay/Push) | covered | Present. | — |
  | `SelectedItem` two-way | covered | Present. | — |
  | `OnClose` event | covered | Present. | — |
  | ARIA `role="navigation"` | covered | Emitted. | — |

- **Verdict**: `needs-work` — the component is largely complete but spec code samples
  that use `Position.Start` or `<Template>` don't compile, and rendering-customisation
  is weaker than spec.

---

## Component: SunfishGridLayout (spec: `gridlayout`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\GridLayout\` (SunfishGridLayout.razor + GridLayoutColumn.razor + GridLayoutRow.razor + GridLayoutItem.razor).
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Layout\GridLayout\Overview\Demo.razor` (no other demo pages).
- **Spec files reviewed**: `overview.md`, `items.md`, `columns.md`, `rows.md`, `spacing.md`,
  `alignment.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<GridLayoutColumns>` / `<GridLayoutRows>` / `<GridLayoutItems>` wrapper tags (overview.md) | missing | Spec wraps column / row / item definitions in three collection tags. Impl exposes `ColumnDefinitions` / `RowDefinitions` RenderFragments and leaves items as direct children. Spec snippet shape doesn't compile. | Add three passthrough wrapper components that append their child content to the parent's definitions list. |
  | `GridLayoutHorizontalAlign` / `GridLayoutVerticalAlign` enum (alignment.md) | incomplete | Spec uses dedicated `GridLayoutHorizontalAlign` / `GridLayoutVerticalAlign` enums (Stretch/Start/Center/End). Impl reuses the generic `StackAlignment` enum. | Introduce the two dedicated enums in Foundation; either map or migrate. |
  | Demo pages (Appearance/Events/Accessibility) | missing | Only Overview demo exists. | Add the three other demo shells. |
  | `GridLayoutItem.Row` / `Column` / `RowSpan` / `ColumnSpan` | covered | Parameters exist. | — |
  | `GridLayoutRow.Height` / `GridLayoutColumn.Width` | covered | Parameters exist. | — |
  | `ColumnSpacing` / `RowSpacing` | covered | Parameters exist. | — |
  | `Class` passthrough | incomplete | Implemented but not emphasised in demo coverage. | — (documentation-only.) |

- **Verdict**: `needs-work` — functionally near-complete; the wrapper-tag shape and
  dedicated alignment enums are the API-parity gaps.

---

## Component: SunfishAccordion (spec: `panelbar`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\Accordion\` (SunfishAccordion.razor + SunfishAccordionItem.razor).
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Layout\Accordion\{Overview,Appearance,Events,Accessibility}\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Layout\PanelBar\Overview\Demo.razor` (alias)
- **Spec files reviewed**: `overview.md`, `data-binding/` (hierarchical-data + flat-data),
  `navigation.md`, `icons.md`, `expanded-items.md`, `events.md`, `refresh-data.md`,
  `templates/`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | **Hierarchical data (tree)** (data-binding/hierarchical-data.md) | missing | Spec's core value-prop is nested children — it models a tree with either `Items` navigation via children-field or `ParentId`/`HasChildren` (self-referential). Impl is **flat-only**; no way to model nested panels. This is the biggest gap. | Add a `ItemsField` plus recursive render; or `ParentIdField`+`HasChildrenField` self-ref support. |
  | `PanelBarBindings` / `PanelBarBinding` child tags (data-binding.md) | missing | Spec maps fields via a `<PanelBarBindings><PanelBarBinding TextField="…"/></PanelBarBindings>` nested tag so multi-level hierarchies can have different field-mappings. Impl only has flat field-name parameters. | Add `SunfishAccordionBindings` + `SunfishAccordionBinding` collection with per-level configuration. |
  | `OnItemClick` event (events.md) | missing | Spec fires an `OnItemClick` with event args (`Item`, `IsExpanded`). Impl fires nothing. | Add event. |
  | `OnExpand` / `OnCollapse` events (events.md) | missing | Spec fires both explicitly. Impl only fires `ExpandedItemsChanged` after toggle. | Add dedicated events. |
  | `OnItemRender` event (events.md) | missing | Per-item render event with `Class` mutation. Impl none. | Add. |
  | `Navigable` / `UrlField` auto-nav (navigation.md) | incomplete | Spec: setting `UrlField` auto-navigates on click. Impl renders `<a href="…">` only in non-template branch and does not integrate with Blazor's `NavigationManager`. | Wire to `NavigationManager` and expose `Navigable` parameter. |
  | `ExpandMode.Single` / `Multiple` | covered | Spec enum matches impl `AccordionExpandMode`. | — |
  | `ExpandedItems` two-way | covered | Present. | — |
  | `role="region"` on items (accessibility) | covered | Emitted. | — |

- **Verdict**: `downgrade-to-partial` — missing hierarchical data + bindings + the event
  surface makes this a surface-level accordion, not a PanelBar. This must be `partial`
  until hierarchy lands.

---

## Component: SunfishSplitter (spec: `splitter`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\Splitter\` (SunfishSplitter.razor + SunfishSplitterPane.razor + SunfishSplitterPanes.razor + SplitterTypes.cs).
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Layout\Splitter\Overview\Demo.razor` only.
- **Spec files reviewed**: `overview.md`, `panes.md`, `orientation.md`, `events.md`, `state.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `SplitterState` / `SplitterPaneState` shape (state.md) | bug | Spec: `SplitterState.Panes: List<SplitterPaneState { Size, Collapsed }>`. Impl: `SplitterState { PaneSizes: List<string>, CollapsedPanes: List<bool> }`. Spec snippets calling `state.Panes[0].Size` don't compile. | Introduce `SplitterPaneState` record; refactor `GetState`/`SetState` to return `Panes`. |
  | `Pane.Visible` (panes.md) | missing | Spec: `Visible` parameter on `SplitterPane` toggles rendering. Impl has no equivalent. | Add `Visible` parameter on `SunfishSplitterPane`. |
  | `Pane.Scrollable` (panes.md) | missing | Spec: per-pane `Scrollable` controls overflow. Impl none. | Add parameter. |
  | `Pane.SizeChanged` / `CollapsedChanged` two-way (panes.md) | missing | Spec supports per-pane `@bind-Size` / `@bind-Collapsed`. Impl fires the splitter-level `OnResize`/`OnCollapse` events only; no pane-level two-way. | Add `SizeChanged` / `CollapsedChanged` event callbacks on pane. |
  | `OnResize` / `OnResizeStart` / `OnResizeEnd` / `OnCollapse` / `OnExpand` | covered | All five present. | — |
  | `GetState` / `SetState` methods | covered | Both present (shape mismatch aside — see row 1). | — |
  | `Orientation` enum (Horizontal/Vertical) | covered | Present. | — |
  | Pointer-based resize JS interop | covered | Implemented and uses shared `IResizeInteractionService`. | — |
  | Pane `Min` / `Max` / `Resizable` / `Collapsible` / `Collapsed` | covered | All present. | — |
  | Demo coverage (Appearance/Events/Accessibility) | incomplete | Only Overview demo exists; events demo would demonstrate the most complex resize interactions. | Add three demo shells. |

- **Verdict**: `needs-work` — functional parity is high; shape mismatch on `SplitterState`
  is a breaking bug for spec-driven code and pane-level `Visible`/`Scrollable`/`SizeChanged`
  are meaningful missing surface.

---

## Component: SunfishStack (spec: `stacklayout`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\SunfishStack.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Layout\Stack\{Overview,Appearance,Accessibility}\Demo.razor`
  - `…\Layout\StackLayout\Overview\Demo.razor` (alias)
- **Spec files reviewed**: `overview.md`, `layout.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Orientation` enum name (overview.md) | bug | Spec: `StackLayoutOrientation` (Horizontal/Vertical). Impl: `StackDirection`. Spec snippets don't compile. | Rename to `StackLayoutOrientation` (can alias `StackDirection`). |
  | `HorizontalAlign` / `VerticalAlign` enum names + values | bug | Spec: `StackLayoutHorizontalAlign` with members `Left`/`Right`/`Center`/`Stretch` (and analogous vertical with `Top`/`Bottom`/`Center`/`Stretch`). Impl reuses the generic `StackAlignment` with `Start`/`End`/`Center`/`Stretch`/`SpaceBetween`/`SpaceAround`. Loses axis-specific semantics and breaks spec sample compilation. | Introduce `StackLayoutHorizontalAlign` + `StackLayoutVerticalAlign`. Keep `StackAlignment` as a lower-level primitive. |
  | Default `HorizontalAlign` / `VerticalAlign` (layout.md) | incomplete | Spec default is `Stretch` for both. Impl default is `Start`. | Change defaults to `Stretch`. |
  | Events demo page (ADR 0022) | missing | No Events demo. | Add placeholder. |
  | `Spacing` | covered | Present. | — |
  | `Width` / `Height` | covered | Present. | — |
  | Nested stacks | covered | No extra work needed (nesting just works). | — |

- **Verdict**: `needs-work` — two enum-name bugs break spec samples; defaults also
  diverge.

---

## Component: SunfishStepper (spec: `stepper`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\Stepper\` (SunfishStepper.razor + SunfishStep.razor).
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Stepper\{Overview,Appearance,Events,Accessibility}\Demo.razor`.
- **Spec files reviewed**: `overview.md`, `display-modes.md`, `orientation.md`,
  `linear-flow.md`, `step-template.md`, `events.md`, `steps/overview.md`, `steps/validation.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Value` / `ValueChanged` parameter name (overview.md) | bug | Spec: `Value` (int) is the current step index with `ValueChanged`. Impl: `ActiveStep` / `ActiveStepChanged`. Spec sample `@bind-Value="@CurrentStepIndex"` doesn't compile. | Add `Value`/`ValueChanged` as the canonical pair; alias `ActiveStep`. |
  | `<StepperSteps>` wrapper tag (overview.md) | missing | Spec wraps `StepperStep` children in a `<StepperSteps>` tag. Impl accepts `StepperStep` directly under the stepper. Spec samples don't compile. | Add a passthrough `SunfishStepperSteps` component. |
  | `StepType` enum (`Steps` / `Labels`) (display-modes.md) | missing | Spec toggles indicator-vs-label-only display via `StepType`. Impl has no such parameter. | Add `StepType` parameter (default `Steps`); hide indicator when `Labels`. |
  | `StepperStep.OnChange` cancellation (events.md) | missing | Spec: each step fires `OnChange` before transition with `StepperStepChangeEventArgs { TargetIndex, IsCancelled }`. Impl has no per-step OnChange; only a root `OnStepClick` (non-cancellable). | Add `OnChange` on `SunfishStep`; wire cancel into `OnIndicatorClick`. |
  | `StepperStep.Valid` (steps/validation.md) | incomplete | Spec: `Valid: bool?` drives success/error icon rendering. Impl: uses `StepStatus?` override (`StepStatus.Error`) — similar semantics but different surface. | Add `Valid` nullable-bool parameter; compose into `GetStepStatus`. |
  | `StepperStep.Label` vs `Title` | incomplete | Spec uses `Label`. Impl uses `Title`. | Rename (alias for compat). |
  | `StepperStep.Text` (steps/overview.md) | incomplete | Spec: separate `Text` parameter controls the indicator text (distinct from `Label`). Impl only has `Icon`/`Title`. | Add `Text` parameter; render when no icon. |
  | `Orientation` enum (Horizontal/Vertical) | covered | Matches spec. | — |
  | `Linear` | covered | Present. | — |
  | Step `Disabled` / `Optional` / `Icon` | covered | Present. | — |
  | `role="tablist"` | covered | Present in CSS provider. | — |

- **Verdict**: `needs-work` — major API-name drift from spec (Value/ValueChanged,
  StepperSteps wrapper, Label vs Title, Text, Valid) means none of the documented
  stepper samples actually compile against the impl.

---

## Component: SunfishTabStrip (spec: `tabstrip`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\TabStrip\` (SunfishTabStrip.razor + TabStripTab.razor).
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\TabStrip\{Overview,Appearance,Events,Accessibility}\Demo.razor`.
- **Spec files reviewed**: `overview.md`, `tabs-configuration.md`, `tabs-position.md`,
  `tabs-alignment.md`, `sizing.md`, `active-tab-index.md`, `events.md`, `state.md`,
  `tab-reorder.md`, `persist-content.md`, `header-template.md`, `scrollable-tabs.md`,
  `tabs-collection.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Drag-and-drop tab reorder wiring (`EnableTabReorder`, `OnTabReorder`, tab-reorder.md) | incomplete | Spec and impl both declare the parameter + event, but the impl's tab-list markup has no draggable handlers / JS wiring — the event is never fired in-browser. | Add `@ondragstart`/`@ondragover`/`@ondrop` handlers plus JS helper, or wire to the shared `IDragService`. |
  | `Size` type (overview.md) | incomplete | Spec: `Size` is `string` bound to `ThemeConstants.TabStrip.Size`. Impl: `Size` is `TabSize` enum. Spec snippet `Size="@ThemeConstants.TabStrip.Size.Medium"` does not compile. | Add a string `Size` alias parameter that maps back to the enum internally, or introduce `ThemeConstants` constants of matching names. |
  | `TabStripTab.Content` + `HeaderTemplate` spec shape (header-template.md) | missing | Spec documents the `<Content>` and `<HeaderTemplate>` child tags on the tab. Impl exposes them as `RenderFragment` parameters only — but needs verified demos and docs. | Verify the existing parameters match spec shape; add a demo that uses both `<HeaderTemplate>` and `<Content>` explicitly. |
  | `ScrollButtonsPosition` / `ScrollButtonsVisibility` end-to-end (scrollable-tabs.md) | missing | Parameters exist but no CSS/interop actually renders scroll buttons (markup only has overflow menu path). | Implement a `Scroll` overflow rendering path: left/right buttons, visible-on-overflow logic. |
  | `ActiveTabId` (events.md + active-tab-index.md) | covered | Present; two-way binding works. | — |
  | `ActiveTabIndex` (obsolete alias) | covered | Kept as `[Obsolete]`. | — |
  | `TabPosition` (Top/Bottom/Left/Right) | covered | Present. | — |
  | `TabAlignment` | covered | Present. | — |
  | `PersistTabContent` | covered | Present; hides inactive tabs via CSS when true. | — |
  | `GetState` / `SetState` | covered | Present; `TabStripState`/`TabStripTabState` shape matches spec. | — |
  | `OnStateInit` / `OnStateChanged` | covered | Both fire at the documented lifecycle points. | — |
  | `TabStripSuffixTemplate` + overflow menu | covered | Menu overflow implemented with `MaxVisibleTabs`. | — |

- **Verdict**: `needs-work` — feature footprint is large and mostly right, but
  reorder is non-functional and `Scroll` overflow path is missing.

---

## Component: SunfishWizard (spec: `wizard`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\Wizard\` (SunfishWizard.razor + SunfishWizardSteps.razor + WizardStep.razor + WizardTypes.cs).
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Wizard\Overview\Demo.razor` (no Appearance/Events/Accessibility).
- **Spec files reviewed**: `overview.md`, `layout.md`, `events.md`, `templates.md`,
  `form-integration.md`, `structure/stepper.md`, `structure/buttons.md`, `structure/content.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<WizardSettings>` / `<WizardStepperSettings>` child tags (structure/stepper.md) | missing | Spec configures the internal stepper via `<WizardSettings><WizardStepperSettings StepType="…" Linear="…"/></WizardSettings>`. Impl puts `Linear` directly on `SunfishWizard` (no `StepType` at all). Spec samples don't compile. | Add `SunfishWizardSettings` and `SunfishWizardStepperSettings` passthrough components that set parent-level parameters via `CascadingValue` / register-on-init. |
  | `StepType` (Steps/Labels) | missing | Spec exposes a StepType on the wizard stepper. Impl has no way to switch to label-only mode. | Inherit from Stepper fix — add `StepType` parameter. |
  | `<StepTemplate>` child tag (templates.md) | incomplete | Spec: `<StepTemplate>` is a nested tag on `<WizardStep>`. Impl exposes `StepTemplate` as a `RenderFragment?` parameter — same intent, but the spec-style `<StepTemplate>…</StepTemplate>` tag form does not compile against a RenderFragment parameter. | Either add a `WizardStepTemplate` child component, or document that users can still write `<StepTemplate>` because Blazor accepts parameters as nested tags of the same name. |
  | `WizardStepperPosition` enum | covered | Matches spec (Top/Bottom/Left/Right). | — |
  | `ValueChanged` | covered | Present. | — |
  | `OnFinish` | covered | Present. | — |
  | Cancellable step change (`OnChange` with `WizardStepChangeEventArgs`) | covered | `WizardStep.OnChange` fires and respects `IsCancelled`. | — |
  | `ShowPager` | covered | Present. | — |
  | `<WizardButtons>` custom buttons context (structure/buttons.md) | incomplete | Spec uses `RenderFragment<int>` keyed on current step index. Impl matches (`WizardButtons` is `RenderFragment<int>`). But spec sample uses `var index = context;` — which works — **but** the fragment name spec uses is `WizardButtons` directly, and demo paths show this; audit only flags that no demo actually exercises it. | Add an Events or Advanced demo using `<WizardButtons>`. |
  | Mapping metadata | missing | Mapping entry for `wizard` is `{ "sunfish": null, "status": "planned" }`. Impl + demo exist. Metadata is stale. | Update mapping to `{ "sunfish": "SunfishWizard", "status": "implemented", "category": "navigation" }`. |
  | Demos Appearance/Events/Accessibility | missing | Only Overview. | Add three demo shells. |
  | ARIA `role="tablist"` / `aria-current="step"` | covered | Implemented. | — |

- **Verdict**: `needs-work` — wizard impl is solid but mapping is wrong and the
  `<WizardSettings>` / `StepType` / demo-shell surface is incomplete.

---

## Component: SunfishWindow (spec: `window`)

- **Impl root**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Overlays\Window\` (SunfishWindow.razor + WindowTitle.razor + WindowActions.razor + WindowActionButton.razor + WindowContent.razor + WindowFooter.razor).
- **Demos**: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Feedback\Window\{Overview,Appearance,Events,Accessibility}\Demo.razor`.
- **Spec files reviewed**: `overview.md`, `size.md`, `position.md`, `actions.md`,
  `modal.md`, `draggable.md`, `animation.md`, `events.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `WindowAction` as child component (actions.md) | incomplete | Spec: `<WindowAction Name="Close" Hidden="…" Icon="…" OnClick="…" Title="…" />`. Impl has `WindowActionButton.razor` and `WindowActions.razor`, but documentation and tag names need verifying against spec (spec component is `<WindowAction>` singular). | Rename `WindowActionButton` → `WindowAction` (or expose both names). Already registers via internal list — matches intent. |
  | `WindowState` enum name (size.md) | incomplete | Spec: `WindowState` (Default/Minimized/Maximized). Impl: `WindowDisplayState` (Normal/Minimized/Maximized). Spec samples don't compile and default-name diverges (`Default` vs `Normal`). | Add `WindowState` enum with `Default` member as alias; map to `WindowDisplayState.Normal` internally. |
  | `FooterLayoutAlign` `Stretch` default (overview.md) | missing | Spec default is `Stretch`. Impl default is `End`. | Change default to `Stretch`. |
  | `Id` parameter (overview.md) | missing | Spec lists `Id`. Impl has none (pass-through via `AdditionalAttributes` only). | Add explicit `[Parameter] Id`. |
  | `Size` enum naming (overview.md) | missing | Spec `Size` is a `string` tied to `ThemeConstants.Window.Size` (Small/Medium/Large). Impl: `Size` is a `WindowSize?` enum. Spec sample `Size="@ThemeConstants.Window.Size.Small"` does not compile. | Add string `Size` alias mapped to enum, or introduce the ThemeConstants class. |
  | `WindowAnimationType` enum | covered | Matches spec (all 11 animation types enumerated). | — |
  | `AnimationDuration` | covered | Present, default 300. | — |
  | `Modal` + `CloseOnOverlayClick` | covered | Both present. | — |
  | `Draggable` + JS interop | covered | Implemented with pointer events. | — |
  | `Resizable` handles | covered | 8 handles. | — |
  | `ContainmentSelector` | covered | Param present; JS `clampPosition` uses it. | — |
  | `ThemeColor` | covered | String param, CSS variable. | — |
  | `MinWidth`/`MaxWidth`/`MinHeight`/`MaxHeight` | covered | All four present. | — |
  | `Refresh()` reference | covered | Present. | — |
  | Event parity (`VisibleChanged`, `StateChanged`, `WidthChanged`, `HeightChanged`, `TopChanged`, `LeftChanged`, `OnAction`) | covered | All present. | — |
  | `PersistContent` | covered | Present. | — |
  | ARIA `role="dialog"` + `aria-modal` + `aria-labelledby` | covered | Emitted. | — |

- **Verdict**: `needs-work` — mapping flags this `partial` and the audit keeps it there.
  Parity is actually very high; fixes are naming-surface (`WindowState` default,
  `WindowAction` tag) and `Id`/`Size` shape. Worth re-tagging to `implemented` once
  these land.

---

## Top-level next actions

1. **Mapping drift** — update `apps/docs/component-specs/component-mapping.json`:
   - `wizard`: `{ "sunfish": "SunfishWizard", "status": "implemented", "category": "navigation" }`.
   - Consider re-classifying `stepper` / `wizard` / `carousel` consistent with the task
     brief, or pin them as multi-category (brief explicitly audits them as "Layout").
   - After the fixes below, `window` should move from `partial` → `implemented`.

2. **Highest-impact parity gaps (Tier 2 priority order)** — fix in roughly this
   sequence (most-blocking to least):

   1. **SunfishAccordion — hierarchical data + PanelBarBindings** (`panelbar`, downgraded
      to partial). Largest spec surface missing; blocks PanelBar adoption.
   2. **SunfishCarousel — `Page`/`AutomaticPageChange`/`Template`/`Rebind`** (`carousel`,
      downgraded). Two bugs break spec samples; plus keyboard a11y.
   3. **SunfishAppBar — child components** (`AppBarSection`/`AppBarSeparator`/`AppBarSpacer`
      + `ThemeColor` + `Refresh`). Downgraded to partial; fix unblocks real-world nav bars.
   4. **SunfishStepper — `Value`/`ValueChanged`/`<StepperSteps>`/`StepType`/`Text`/`Valid`**.
      None of the spec code samples compile against impl today.
   5. **SunfishSplitter — `SplitterState.Panes[]` shape** (bug) + pane-level `Visible` /
      `Scrollable` / two-way size & collapse.
   6. **SunfishDialog — `<DialogTitle>` tag + `DialogButtons`/`DialogButtonsLayout` +
      `DialogFactory` service**. Brings predefined Alert/Confirm/Prompt online.
   7. **SunfishGridLayout — `<GridLayoutColumns>`/`<GridLayoutRows>`/`<GridLayoutItems>`
      wrappers + dedicated alignment enums**.
   8. **SunfishDrawer — `DrawerPosition.Start`/`End` + `<Template>` full-drawer fragment +
      `SeparatorField`/`OnItemRender`**.
   9. **SunfishTabStrip — complete drag-and-drop reorder wiring + `Scroll` overflow path**.
   10. **SunfishStack — `StackLayoutOrientation`/`StackLayoutHorizontalAlign`/`StackLayoutVerticalAlign`
       enums + `Stretch` defaults**.
   11. **SunfishWindow — `WindowState` enum + string-`Size` alias + `<WindowAction>` tag
       name + `FooterLayoutAlign=Stretch` default + explicit `Id`**. Then re-tag to `implemented`.
   12. **SunfishCard — `<CardSeparator>`/`<CardTitle>`/`<CardSubTitle>` + `ThemeColor`**.
   13. **SunfishWizard — `<WizardSettings>`/`<WizardStepperSettings>`/`StepType`**.

3. **Demo-shell coverage (ADR 0022 Tier 1)** — add missing `Appearance/`, `Events/`,
   `Accessibility/` Demo.razor pages for AppBar, Carousel, GridLayout, Splitter, StackLayout,
   Wizard. Empty-but-present is acceptable for components without meaningful events.

4. **Common cross-cutting finding** — spec uses a `ThemeColor` string parameter
   (bound to `ThemeConstants.<Component>.ThemeColor`) on many components (AppBar, Card,
   Carousel, Dialog, Window already covered). A `ThemeColor` convention should be part of
   the Sunfish Blazor component base. Track as a shared work item in ADR 0022 follow-up.
