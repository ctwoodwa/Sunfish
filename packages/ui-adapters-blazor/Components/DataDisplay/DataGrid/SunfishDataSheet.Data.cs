using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Helpers;
using Sunfish.Foundation.Models.DataSheet;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Dirty-state tracking, commit, reset, and validation logic for SunfishDataSheet.
/// </summary>
public partial class SunfishDataSheet<TItem>
{
    // ── Dirty State ────────────────────────────────────────────────────

    internal readonly Dictionary<object, DirtyRowEntry<TItem>> _dirtyRows = [];

    internal class DirtyRowEntry<T>
    {
        public T Original { get; set; } = default!;
        public T Current { get; set; } = default!;
        public HashSet<string> DirtyFields { get; } = [];
        public Dictionary<string, string> ValidationErrors { get; } = [];
        public bool IsDeleted { get; set; }

        // V05.3 — Rows created by AddRowAsync are flagged so ResetAsync can
        // remove them from _displayRows entirely instead of trying to restore
        // nonexistent "original" values.
        public bool IsNewlyAdded { get; set; }

        // V02.2 / V05.1 — Transient state override for Save All lifecycle.
        // When set, GetCellState returns this value instead of the computed
        // Invalid/Dirty/Pristine state. SaveAllAsync assigns CellState.Saving
        // before OnSaveAll fires and CellState.Saved after it succeeds, then
        // clears the override to complete the transition to Pristine.
        public CellState? TransientState { get; set; }

        public CellState OverallState => TransientState
                                        ?? (ValidationErrors.Count > 0 ? CellState.Invalid
                                        : DirtyFields.Count > 0 ? CellState.Dirty
                                        : CellState.Pristine);
    }

    // V02.2 / V05.1 — Duration of the CellState.Saved visual indicator
    // before cells transition to Pristine. Kept configurable internally so
    // tests can shrink it to zero for deterministic assertions.
    internal int _savedStateDurationMs = 1000;

    // V02.2 / V05.1 — Re-entrancy guard for SaveAllAsync. The Saved
    // visual-indicator window (Task.Delay(_savedStateDurationMs)) is long
    // enough that a second SaveAllAsync call during that window could race
    // the Step 7 cleanup and stomp on rows the user just re-dirtied. Silent
    // return matches standard UX-control double-click handling.
    private bool _isSaving;

    // ── Key Resolution ─────────────────────────────────────────────────

    internal object? GetRowKey(TItem item)
    {
        return GridReflectionHelper.GetValue(item, KeyField);
    }

    // ── Cell Commit ────────────────────────────────────────────────────

    /// <summary>Commits a cell value change and updates dirty state.</summary>
    public async Task CommitCellEdit(TItem row, string field, object? newValue)
    {
        var key = GetRowKey(row);
        if (key is null) return;

        // V07.9 — Capture the dirty-row count BEFORE mutating the dirty
        // state so we can fire a screen-reader announcement only when the
        // count actually changes. Edits within an already-dirty row must
        // not spam the aria-live region with a per-keystroke announcement.
        var priorDirtyRowCount = _dirtyRows.Values
            .Count(e => e.DirtyFields.Count > 0 && !e.IsDeleted);

        // Get or create dirty entry. The TryGetValue result doubles as
        // "is this the first touch on this row?" — on a first touch we
        // snapshot the current row as Original before any mutation.
        var hadExistingEntry = _dirtyRows.TryGetValue(key, out var entry);
        if (!hadExistingEntry)
        {
            entry = new DirtyRowEntry<TItem>
            {
                Original = GridReflectionHelper.DeepClone(row),
                Current = row
            };
            _dirtyRows[key] = entry;
        }

        var oldValue = GridReflectionHelper.GetValue(row, field);
        var originalValue = GridReflectionHelper.GetValue(entry!.Original, field);

        // Set the new value
        GridReflectionHelper.SetValue(row, field, newValue);

        // NOTE: object.Equals uses reference equality for user POCOs that do
        // not override Equals. For value types and strings this is correct;
        // for reference-typed cell values the caller must override Equals
        // or the revert detection will not fire.
        // V05.3 — Newly-added rows keep their dirty fields regardless of
        // value comparison: the "original" snapshot is the default TItem,
        // so a user typing the default value should NOT drop the field.
        // First-touch edits always add the field too; the very first commit
        // on a previously-clean row means the cell IS being touched.
        var revertedToOriginal = object.Equals(newValue, originalValue);
        if (hadExistingEntry && !entry.IsNewlyAdded && revertedToOriginal)
        {
            entry.DirtyFields.Remove(field);
        }
        else
        {
            entry.DirtyFields.Add(field);
        }

        // Run column validation
        var column = _columns.FirstOrDefault(c => c.Field == field);
        if (column != null)
        {
            var error = RunColumnValidation(column, row);
            if (error != null)
                entry.ValidationErrors[field] = error;
            else
                entry.ValidationErrors.Remove(field);
        }

        // If the row has no remaining dirty fields and is not deleted or
        // newly added, remove its entry entirely so state queries report
        // Pristine. Newly added rows are preserved so SaveAllAsync still
        // emits them in DataSheetSaveArgs.DirtyRows.
        if (entry.DirtyFields.Count == 0 && !entry.IsDeleted && !entry.IsNewlyAdded)
        {
            _dirtyRows.Remove(key);
        }

        // Fire OnRowChanged
        if (OnRowChanged.HasDelegate)
        {
            await OnRowChanged.InvokeAsync(new DataSheetRowChangedArgs<TItem>
            {
                Row = row,
                Field = field,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        // V07.9 — Announce dirty count changes to screen readers via the
        // aria-live region. Spec keyboard-and-accessibility.md:150 —
        // "{N} rows modified" when the dirty row count changes. Only
        // fire when the count transitions; per-field edits on an already
        // dirty row don't re-announce.
        var newDirtyRowCount = _dirtyRows.Values
            .Count(e => e.DirtyFields.Count > 0 && !e.IsDeleted);
        if (newDirtyRowCount != priorDirtyRowCount)
        {
            _ariaAnnouncement = newDirtyRowCount == 1
                ? "1 row modified"
                : $"{newDirtyRowCount} rows modified";
        }

        StateHasChanged();
    }

    // ── Validation ─────────────────────────────────────────────────────

    internal string? RunColumnValidation(SunfishDataSheetColumn<TItem> column, TItem row)
    {
        // Required check
        if (column.Required)
        {
            var value = GridReflectionHelper.GetValue(row, column.Field);

            // Checkbox: a required checkbox must be checked (true).
            if (column.ColumnType == DataSheetColumnType.Checkbox)
            {
                if (value is null || value is false)
                    return $"{column.DisplayTitle} is required.";
            }
            else if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                return $"{column.DisplayTitle} is required.";
            }
        }

        // Custom validate func
        if (column.Validate != null)
        {
            return column.Validate(row);
        }

        return null;
    }

    /// <summary>Runs full grid validation. Returns true if all cells are valid.</summary>
    public async Task<bool> ValidateAllAsync()
    {
        var hasErrors = false;

        foreach (var entry in _dirtyRows.Values)
        {
            if (entry.IsDeleted) continue;

            foreach (var column in _columns)
            {
                if (!column.Editable || column.ColumnType == DataSheetColumnType.Computed) continue;

                var error = RunColumnValidation(column, entry.Current);
                if (error != null)
                {
                    entry.ValidationErrors[column.Field] = error;
                    hasErrors = true;
                }
                else
                {
                    entry.ValidationErrors.Remove(column.Field);
                }
            }
        }

        // Also validate required fields on all dirty rows
        StateHasChanged();
        await Task.CompletedTask;
        return !hasErrors;
    }

    // ── Save All ───────────────────────────────────────────────────────

    /// <summary>
    /// Triggers the Save All flow: validate, then fire OnSaveAll.
    /// </summary>
    /// <remarks>
    /// <para>
    /// F2.M3 — On success this method calls <c>GridReflectionHelper.DeepClone</c>
    /// on each non-deleted dirty row so a subsequent edit that reverts to the
    /// just-saved value is still correctly detected as Dirty. <typeparamref name="TItem"/>
    /// must therefore be deep-cloneable by the project's reflection helper —
    /// plain POCOs with public settable properties work out of the box. Types
    /// that hold non-serializable members (streams, connections, delegates)
    /// should either expose a shallow-clone constructor or avoid being used
    /// as <see cref="SunfishDataSheet{TItem}"/> row types.
    /// </para>
    /// <para>
    /// <b>Warning:</b> Do NOT call <see cref="ResetAsync"/>, <c>BulkResetAsync</c>,
    /// or any other state-clearing method from within your <see cref="OnSaveAll"/>
    /// handler. The component automatically removes deleted rows and clears dirty
    /// state after your handler returns (Step 6/7 below). Calling reset methods
    /// from inside the handler would clobber this cleanup and silently drop
    /// pending deletions, because the subsequent cleanup step recomputes deleted
    /// keys from the now-empty dirty dictionary and removes nothing.
    /// </para>
    /// <para>
    /// If you need to react to the save result (e.g., show a toast notification
    /// or log an audit event), do so using simple statements in the handler —
    /// the component handles all state cleanup automatically.
    /// </para>
    /// </remarks>
    public async Task SaveAllAsync()
    {
        if (_dirtyRows.Count == 0) return;

        // V02.2 / V05.1 — Re-entrancy guard. A second call during the
        // Step 7 Saved-indicator window would race the cleanup and stomp
        // on rows the user may have re-dirtied in the meantime. Silent
        // return matches standard UX double-click handling.
        if (_isSaving) return;
        _isSaving = true;

        // SA-13: announce save start to screen readers
        _ariaAnnouncement = "Saving changes.";
        StateHasChanged();

        // Track entries whose TransientState we set to Saving so the
        // exception path can roll them back cleanly.
        List<DirtyRowEntry<TItem>>? savingEntries = null;

        try
        {
            // Step 1: Validate all
            var isValid = await ValidateAllAsync();

            // Step 2: Fire OnValidate for consumer-side validation
            var dirtyRowsList = _dirtyRows.Values
                .Where(e => e.DirtyFields.Count > 0 && !e.IsDeleted)
                .Select(e => e.Current)
                .ToList();

            if (OnValidate.HasDelegate)
            {
                var validateArgs = new DataSheetValidateArgs<TItem>
                {
                    DirtyRows = dirtyRowsList
                };
                await OnValidate.InvokeAsync(validateArgs);

                // Apply consumer errors to entries
                foreach (var error in validateArgs.Errors)
                {
                    var errorKey = GetRowKey(error.Row);
                    if (errorKey != null && _dirtyRows.TryGetValue(errorKey, out var entry))
                    {
                        entry.ValidationErrors[error.Field] = error.Message;
                        isValid = false;
                    }
                }
            }

            // Step 3: Block if invalid
            if (!isValid)
            {
                // SA-13: announce validation failure with error count
                var errorCount = _dirtyRows.Values
                    .SelectMany(e => e.ValidationErrors)
                    .Count();
                _ariaAnnouncement = $"Save failed. {errorCount} validation error{(errorCount == 1 ? "" : "s")}.";
                StateHasChanged();
                return;
            }

            // Step 4: Mark non-deleted dirty entries as Saving so the cell state
            // transitions from Dirty -> Saving before OnSaveAll fires. (V02.2 / V05.1)
            savingEntries = new List<DirtyRowEntry<TItem>>();
            foreach (var entry in _dirtyRows.Values)
            {
                if (!entry.IsDeleted)
                {
                    entry.TransientState = CellState.Saving;
                    savingEntries.Add(entry);
                }
            }
            StateHasChanged();

            // Step 5: Fire OnSaveAll. If the consumer handler throws, the
            // catch block below rolls every snapshotted entry back to its
            // dirty-but-editable state so the user can retry.
            if (OnSaveAll.HasDelegate)
            {
                var deletedRows = _dirtyRows.Values
                    .Where(e => e.IsDeleted)
                    .Select(e => e.Current)
                    .ToList();

                // Consumers must not mutate dirty/deleted state from within
                // the handler (no ResetAsync/BulkResetAsync calls). Step 6
                // below relies on _dirtyRows still containing deletion
                // markers so it can remove the deleted rows from _displayRows.
                await OnSaveAll.InvokeAsync(new DataSheetSaveArgs<TItem>
                {
                    DirtyRows = dirtyRowsList,
                    DeletedRows = deletedRows
                });
            }

            // Step 6: On success, remove deleted rows from _displayRows (V05.2),
            // update original snapshots for saved dirty rows so subsequent edits
            // that revert to the just-saved value are correctly dirty-tracked,
            // and mark dirty entries as Saved for a brief visual indicator.
            // F2.M5 — Snapshot the deleted keys in one LINQ pass; subsequent
            // _displayRows.RemoveAll and _dirtyRows.Remove iterations need the
            // full set up-front (we cannot mutate _dirtyRows while iterating it).
            var deletedKeys = _dirtyRows
                .Where(kv => kv.Value.IsDeleted)
                .Select(kv => kv.Key)
                .ToHashSet();

            if (deletedKeys.Count > 0)
            {
                _displayRows.RemoveAll(row =>
                {
                    var k = GetRowKey(row);
                    return k != null && deletedKeys.Contains(k);
                });
                foreach (var k in deletedKeys)
                {
                    _dirtyRows.Remove(k);
                }
            }

            // For remaining (non-deleted) dirty entries, update the original
            // snapshot to reflect the just-saved current values and flip to
            // CellState.Saved for the brief visual indicator period.
            var savedKeys = _dirtyRows.Keys.ToList();
            foreach (var key in savedKeys)
            {
                if (_dirtyRows.TryGetValue(key, out var entry))
                {
                    entry.Original = GridReflectionHelper.DeepClone(entry.Current);
                    entry.IsNewlyAdded = false;
                    entry.TransientState = CellState.Saved;
                }
            }

            _ariaAnnouncement = "Changes saved successfully.";
            StateHasChanged();

            // Step 7: After a brief visual indicator period, clear the
            // TransientState and drop the entries so cells report Pristine.
            if (_savedStateDurationMs > 0)
            {
                await Task.Delay(_savedStateDurationMs);
            }

            foreach (var key in savedKeys)
            {
                if (_dirtyRows.TryGetValue(key, out var entry) && entry.TransientState == CellState.Saved)
                {
                    entry.TransientState = null;
                    entry.DirtyFields.Clear();
                    entry.ValidationErrors.Clear();
                    _dirtyRows.Remove(key);
                }
            }
            StateHasChanged();
        }
        catch
        {
            // SA-13: announce save failure to screen readers
            _ariaAnnouncement = "Save failed. An error occurred.";

            // Rollback: clear TransientState on every entry we flipped to
            // Saving so the grid returns to a dirty-but-editable state and
            // the user can retry. Then re-throw so the caller sees the
            // original failure.
            // The null-guard intentionally skips rollback for exceptions
            // thrown in Steps 1-3 (validate / OnValidate): savingEntries is
            // only populated in Step 4, so a null value means no entry ever
            // flipped to CellState.Saving and there is nothing to roll back.
            if (savingEntries != null)
            {
                foreach (var entry in savingEntries)
                {
                    entry.TransientState = null;
                }
            }
            StateHasChanged();
            throw;
        }
        finally
        {
            _isSaving = false;
        }
    }

    // ── Reset ──────────────────────────────────────────────────────────

    // F2.M1 — Shared restore/remove body used by both ResetAsync (whole grid)
    // and BulkResetAsync (selected rows). A newly-added entry is removed from
    // _displayRows entirely because its Original snapshot is a default TItem
    // and there is nothing meaningful to revert to; a regular dirty entry
    // has its edited fields restored from the Original snapshot. This helper
    // does NOT drop the dirty-row entry itself — the caller is responsible for
    // clearing _dirtyRows because the two call sites differ in how they do it
    // (ResetAsync uses a bulk Clear, BulkResetAsync removes per selected key).
    /// <summary>
    /// Restores a dirty entry's edited fields from its <see cref="DirtyRowEntry{TItem}.Original"/>
    /// snapshot, or removes newly-added rows from <c>_displayRows</c> entirely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This helper handles two cases:
    /// <list type="bullet">
    /// <item><description><b>Newly-added rows</b> (<see cref="DirtyRowEntry{TItem}.IsNewlyAdded"/> is <c>true</c>):
    /// removed from <c>_displayRows</c> because their <c>Original</c> snapshot is a default
    /// <typeparamref name="TItem"/> and there is nothing meaningful to revert to.</description></item>
    /// <item><description><b>Dirty non-new rows</b>: each dirty field is copied from
    /// <c>Original</c> back onto <c>Current</c>, un-doing the edit.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Edge case — <c>IsNewlyAdded &amp;&amp; IsDeleted</c>:</b> an entry that is BOTH newly-added
    /// AND flagged deleted is treated as newly-added here (i.e. removed from <c>_displayRows</c>)
    /// because the <c>IsNewlyAdded</c> check runs first and returns early. The two public call
    /// sites differ in whether this branch is ever hit for the combined state:
    /// <list type="bullet">
    /// <item><description><see cref="ResetAsync"/> guards on <c>IsDeleted</c> before calling this
    /// helper, so a <c>new+deleted</c> entry never reaches here via <c>ResetAsync</c> — instead the
    /// row stays visible and its dirty entry is simply cleared.</description></item>
    /// <item><description><c>BulkResetAsync</c> does NOT guard on <c>IsDeleted</c> — every selected
    /// row is routed through this helper, so a <c>new+deleted</c> entry reset via
    /// <c>BulkResetAsync</c> IS removed from <c>_displayRows</c>.</description></item>
    /// </list>
    /// This asymmetry is intentional and safe: in both flows the final visible state is
    /// consistent with what a user would expect — Reset un-deletes a pure-deleted row and drops
    /// purely-new rows; Bulk Reset Selected removes new rows from the selection regardless of
    /// their deletion flag. The helper's remove-on-<c>IsNewlyAdded</c> behavior is the only
    /// sensible outcome for the combined state if a caller ever reaches it directly, since a
    /// newly-added row has no prior state to restore.
    /// </para>
    /// </remarks>
    internal void RestoreEntryOrRemoveNewRow(DirtyRowEntry<TItem> entry)
    {
        if (entry.IsNewlyAdded)
        {
            _displayRows.Remove(entry.Current);
            return;
        }

        foreach (var field in entry.DirtyFields)
        {
            var originalValue = GridReflectionHelper.GetValue(entry.Original, field);
            GridReflectionHelper.SetValue(entry.Current, field, originalValue);
        }
    }

    /// <summary>Discards all dirty state and restores original values.</summary>
    public Task ResetAsync()
    {
        // V05.3 — Newly-added rows have no meaningful Original snapshot and
        // cannot be "restored" to anything — the spec says added rows are
        // removed on reset. F2.M1 routes both paths through the shared helper.
        foreach (var entry in _dirtyRows.Values)
        {
            if (entry.IsDeleted)
            {
                // Deleted rows stay in _displayRows (reset un-deletes them)
                // and their dirty entry is wiped by the Clear below.
                // NOTE: this guard also swallows the rare combined state
                // IsNewlyAdded && IsDeleted — in ResetAsync such entries skip
                // the helper and simply have their dirty flag cleared, leaving
                // the new row visible. BulkResetAsync behaves differently for
                // that edge case because it does not guard on IsDeleted; see
                // the remarks on RestoreEntryOrRemoveNewRow for the full story.
                continue;
            }

            RestoreEntryOrRemoveNewRow(entry);
        }

        _dirtyRows.Clear();
        _undoBuffer.Clear(); // SA-04: clear undo buffer on reset
        ClearActiveCell();
        _ariaAnnouncement = "All changes have been reset.";
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── Delete ─────────────────────────────────────────────────────────

    internal void MarkRowDeleted(TItem row)
    {
        var key = GetRowKey(row);
        if (key is null) return;

        if (!_dirtyRows.TryGetValue(key, out var entry))
        {
            entry = new DirtyRowEntry<TItem>
            {
                Original = GridReflectionHelper.DeepClone(row),
                Current = row
            };
            _dirtyRows[key] = entry;
        }

        // V05.4 — Toggle delete state. Clicking delete again on a row that
        // is already marked for deletion restores it to its prior editable
        // state. If the row has no other dirty tracking (no dirty fields,
        // not newly added), drop the entry entirely so it goes back to
        // Pristine instead of lingering as an empty entry.
        entry.IsDeleted = !entry.IsDeleted;

        if (!entry.IsDeleted
            && !entry.IsNewlyAdded
            && entry.DirtyFields.Count == 0
            && entry.ValidationErrors.Count == 0)
        {
            _dirtyRows.Remove(key);
        }
    }

    // ── State Queries ──────────────────────────────────────────────────

    internal CellState GetCellState(TItem row, string field)
    {
        var key = GetRowKey(row);
        if (key is null || !_dirtyRows.TryGetValue(key, out var entry))
            return CellState.Pristine;

        // V02.2 / V05.1 — A transient Save All override applies to every
        // dirty cell on the row while the save is in flight (Saving) or
        // briefly after it succeeds (Saved). It wins over the computed
        // Invalid/Dirty states but only for fields that are actually in
        // DirtyFields; non-dirty fields on the row remain Pristine.
        if (entry.TransientState is { } transient && entry.DirtyFields.Contains(field))
        {
            return transient;
        }

        if (entry.ValidationErrors.ContainsKey(field))
            return CellState.Invalid;

        if (entry.DirtyFields.Contains(field))
            return CellState.Dirty;

        return CellState.Pristine;
    }

    internal bool IsRowDirty(TItem row)
    {
        var key = GetRowKey(row);
        if (key is null || !_dirtyRows.TryGetValue(key, out var entry))
            return false;
        return entry.DirtyFields.Count > 0;
    }

    internal bool IsRowDeleted(TItem row)
    {
        var key = GetRowKey(row);
        if (key is null || !_dirtyRows.TryGetValue(key, out var entry))
            return false;
        return entry.IsDeleted;
    }

    internal string? GetCellError(TItem row, string field)
    {
        var key = GetRowKey(row);
        if (key is null || !_dirtyRows.TryGetValue(key, out var entry))
            return null;
        return entry.ValidationErrors.GetValueOrDefault(field);
    }
}
