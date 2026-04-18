# SunfishGantt Resolution Status

## Pass 1 — Spec alignment (complete)
Spec files in `docs/component-specs/gantt/` aligned with source. ~60 gaps closed via documentation.

## Pass 2 — E1–E8 (complete, 648 tests)

| ID | Description | Status | Files |
|----|-------------|--------|-------|
| E1 | Milestone rendering (◆ for zero-duration tasks) | ✅ Complete | SunfishGantt.razor |
| E2 | Summary task auto-calculation (parent dates/percent from children) | ✅ Complete | SunfishGantt.razor.cs, GanttNode.cs |
| E3 | GanttState Phase 2 (EditItem, EditField wired to state events) | ✅ Complete | GanttState.cs, SunfishGantt.razor.cs |
| E4 | Hierarchical data binding (ItemsField, HasChildrenField) | ✅ Complete | GanttFieldAccessor.cs, SunfishGantt.razor.cs |
| E5 | In-cell edit mode (click cell, Tab/Enter/Escape) | ✅ Complete | SunfishGantt.razor, SunfishGantt.razor.cs |
| E6 | Filter menu (popup per column header with funnel icon) | ✅ Complete | SunfishGantt.razor, SunfishGantt.razor.cs, GanttState.cs |
| E7 | Gantt SCSS for both providers + prefers-reduced-motion + forced-colors | ✅ Complete | SCSS files |
| E8 | Spec updates for all 7 features | ✅ Complete | docs/component-specs/gantt/ |

## Pass 3 — E9–E17 (complete, 700 tests)

| ID | Description | Status | Files |
|----|-------------|--------|-------|
| E9 | GanttState.OriginalEditItem clone (IGanttCloneable + JSON fallback) | ✅ Complete | IGanttCloneable.cs, GanttCloneHelper.cs, SunfishGantt.razor.cs |
| E10 | GanttDependencies component model (stub, no SVG rendering) | ✅ Complete | GanttDependency.cs, GanttDependencyType.cs, GanttDependencyEventArgs.cs, SunfishGanttDependencies.razor, SunfishGantt.razor.cs |
| E11 | Screen reader announcements (non-drag) | ✅ Complete | SunfishGantt.razor, SunfishGantt.razor.cs, _gantt.scss, _bridge-gantt.scss |
| E12 | Filter checkbox list (Drawer-hosted, no popup anchoring) | ✅ Complete | GanttState.cs (GanttColumnFilterType enum), GanttColumn.razor, SunfishGantt.razor, SunfishGantt.razor.cs |
| E13 | marilo-drag.ts CDW workspace + API design (design only) | ✅ Complete | ICM/workspaces/marilo-drag/ (full workspace scaffold + API design doc) |
| E14 | SunfishPopup primitive (stub, no full anchor tracking) | ✅ Complete | Overlays/SunfishPopup.razor, SunfishPopup.razor.cs, PopupPlacement.cs, docs/component-specs/popup/overview.md |
| E15 | Popup edit mode (using SunfishPopup stub) | ✅ Complete | GanttState.cs (Popup enum value), SunfishGantt.razor, SunfishGantt.razor.cs |
| E16 | Filter checkbox list via SunfishPopup | ✅ Complete | GanttState.cs (GanttFilterPopupMode enum), SunfishGantt.razor, SunfishGantt.razor.cs |
| E17 | Column chooser using SunfishPopup | ✅ Complete | GanttState.cs (VisibleColumns), SunfishGantt.razor, SunfishGantt.razor.cs |

## Pass 4 — Stage 05 Implementation (InsertedItem/ParentItem wiring, dependency API, test coverage)

| ID | Description | Status | Files |
|----|-------------|--------|-------|
| S05-A | GanttState InsertedItem + ParentItem wiring to GetState/SetStateAsync | ✅ Complete | SunfishGantt.razor.cs |
| S05-B | GanttDependencies component: field mapping parameters (IdField, PredecessorIdField, SuccessorIdField, TypeField) + convenience event args | ✅ Complete | SunfishGanttDependencies.razor, GanttDependencyEventArgs.cs |
| S05-C | Accessibility: validate existing aria-live announcements + add test coverage | ✅ Complete | SunfishGanttTests.cs (tests only — announcements already implemented) |
| S05-D | Filter checkbox list: validate existing implementation + add test coverage | ✅ Complete | SunfishGanttTests.cs (tests only — filtering already implemented) |

### Tests Added (Pass 4)
- OriginalEditItem_PreservesOriginalValues_AfterEditValuesMutated
- OriginalEditItem_PreservesValues_ThroughCancelFlow
- GetState_InsertedItem_NullByDefault
- SetStateAsync_AppliesInsertedItemAndParentItem
- GanttCloneHelper_ReturnsNull_ForNullInput
- Dependencies_Component_Registers_With_Parent
- Dependencies_Component_GetDependencies_ReturnsData
- Dependencies_Component_Default_FieldMapping_MatchesGanttDependencyProperties
- GanttDependency_Record_HasCorrectDefaults
- GanttDependencyCreateEventArgs_ConvenienceProperties_MatchDependency
- GanttDependencyDeleteEventArgs_Item_ReturnsDependency
- GanttDependencyType_HasAllFourTypes
- CommitEdit_Announces_FieldUpdated
- KeyboardNavigation_ArrowDown_Announces_TaskNameAndPosition
- SkipLinks_Render_For_TasklistAndTimeline
- Announcer_Starts_Empty
- CheckboxFilter_GetState_ReflectsAppliedFilter
- CheckboxFilter_SelectAll_EqualsNoFilter
- CheckboxFilter_ColumnFilterType_DefaultIsText

## Deferred to Pass 5+
- Column reorder (JS drag interop required)
- Column resize (JS drag interop required)
- Timeline bar drag-move (JS drag interop required)
- Timeline bar resize (JS drag interop required)
- GanttDependencies SVG rendering
- RangeSnapTo / zooming
- Full anchor-tracked popup (Floating UI)
- Drag-specific screen reader announcements
