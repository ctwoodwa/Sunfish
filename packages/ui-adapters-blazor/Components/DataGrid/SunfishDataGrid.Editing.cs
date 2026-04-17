using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.DataGrid;

/// <summary>
/// Editing/CRUD operations for SunfishDataGrid.
/// Supports Inline, InCell, and Popup edit modes.
/// </summary>
public partial class SunfishDataGrid<TItem>
{
    /// <summary>Begins editing the specified item.</summary>
    public async Task BeginEdit(TItem item)
    {
        if (EditMode == GridEditMode.None) return;
        _originalItem = item;
        _editingItem = item;
        _isCreating = false;
        _inCellEditingField = null;
        if (OnEdit.HasDelegate)
            await OnEdit.InvokeAsync(new GridEditEventArgs<TItem> { Item = item });
        StateHasChanged();
    }

    /// <summary>Begins editing a specific cell (InCell mode only).</summary>
    public async Task BeginCellEdit(TItem item, string field)
    {
        if (EditMode != GridEditMode.InCell) return;
        _originalItem = item;
        _editingItem = item;
        _inCellEditingField = field;
        _isCreating = false;
        if (OnEdit.HasDelegate)
            await OnEdit.InvokeAsync(new GridEditEventArgs<TItem> { Item = item });
        StateHasChanged();
    }

    /// <summary>Begins adding a new item. Fires OnModelInit to get a blank model.</summary>
    public async Task BeginAdd()
    {
        if (EditMode == GridEditMode.None) return;
        var initArgs = new GridModelInitEventArgs<TItem>();
        if (OnModelInit.HasDelegate)
            await OnModelInit.InvokeAsync(initArgs);
        _editingItem = initArgs.Item;
        _isCreating = true;
        _inCellEditingField = null;
        if (OnAdd.HasDelegate)
            await OnAdd.InvokeAsync(new GridEditEventArgs<TItem> { Item = _editingItem });
        StateHasChanged();
    }

    /// <summary>Saves the current edit (create or update).</summary>
    public async Task SaveEdit()
    {
        if (_editingItem == null) return;
        var args = new GridEditEventArgs<TItem> { Item = _editingItem };
        if (_isCreating)
        {
            if (OnCreate.HasDelegate) await OnCreate.InvokeAsync(args);
        }
        else
        {
            if (OnUpdate.HasDelegate) await OnUpdate.InvokeAsync(args);
        }
        if (!args.IsCancelled)
        {
            _editingItem = default;
            _isCreating = false;
            _inCellEditingField = null;
            await ProcessDataAsync();
        }
        StateHasChanged();
    }

    /// <summary>Cancels the current edit operation.</summary>
    public async Task CancelEdit()
    {
        if (_editingItem == null) return;
        if (OnCancel.HasDelegate)
            await OnCancel.InvokeAsync(new GridEditEventArgs<TItem> { Item = _editingItem });
        _editingItem = default;
        _isCreating = false;
        _inCellEditingField = null;
        StateHasChanged();
    }

    /// <summary>Deletes the specified item.</summary>
    public async Task DeleteItem(TItem item)
    {
        if (ConfirmDelete)
        {
            var confirmed = await JS.InvokeAsync<bool>("confirm", new object[] { ConfirmDeleteText });
            if (!confirmed) return;
        }
        var args = new GridEditEventArgs<TItem> { Item = item };
        if (OnDelete.HasDelegate) await OnDelete.InvokeAsync(args);
        if (!args.IsCancelled)
            await ProcessDataAsync();
        StateHasChanged();
    }

    /// <summary>Executes a custom command on a row.</summary>
    public async Task ExecuteCommand(string commandId, TItem? item = default)
    {
        if (OnCommand.HasDelegate)
            await OnCommand.InvokeAsync(new GridCommandEventArgs<TItem> { CommandId = commandId, Item = item });
    }

    /// <summary>Checks if a specific cell is in edit mode (InCell mode).</summary>
    internal bool IsCellEditing(TItem item, string field)
    {
        return EditMode == GridEditMode.InCell
               && _editingItem != null
               && EqualityComparer<TItem>.Default.Equals(_editingItem, item)
               && _inCellEditingField == field;
    }

    // ── Detail row expansion ────────────────────────────────────────────

    /// <summary>Toggles the detail row expansion for the specified item.</summary>
    internal async Task ToggleDetailRow(TItem item)
    {
        if (_expandedDetailItems.Contains(item))
        {
            _expandedDetailItems.Remove(item);
            if (OnRowCollapse.HasDelegate)
                await OnRowCollapse.InvokeAsync(item);
        }
        else
        {
            _expandedDetailItems.Add(item);
            if (OnRowExpand.HasDelegate)
                await OnRowExpand.InvokeAsync(item);
        }
        await NotifyStateChanged("DetailExpand");
    }
}
