---
title: Events
page_title: TreeView - Events
description: Events of the TreeView for Blazor.
slug: treeview-events
tags: sunfish,blazor,treeview,events
published: True
position: 20
components: ["treeview"]
---
# TreeView Events

This article explains the events available in the Sunfish TreeView for Blazor:

* [CheckedItemsChanged](#checkeditemschanged)
* [ExpandedItemsChanged](#expandeditemschanged)
* [OnItemClick](#onitemclick)
* [OnItemContextMenu](#onitemcontextmenu)
* [OnItemEdit](#onitemedit)
* [OnItemDrop](#onitemdrop)
* [SelectedItemsChanged](#selecteditemschanged)

## CheckedItemsChanged

The `CheckedItemsChanged` event fires every time the user uses a [checkbox](slug:treeview-checkboxes-overview) to select a new item.

## ExpandedItemsChanged

The `ExpandedItemsChanged` event fires every time the user expands or collapses a TreeView item.

@[template](/_contentTemplates/common/general-info.md#rerender-after-event)
@[template](/_contentTemplates/common/general-info.md#event-callback-can-be-async)
## OnItemClick

@[template](/_contentTemplates/common/click-events.md#clickeventargs)
The `OnItemClick` event fires when the user clicks a TreeView node (item). The event handler receives `EventCallback<object>` — the data item itself.

## OnItemContextMenu

The `OnItemContextMenu` event fires when the user right-clicks on a TreeView node.

The event handler receives a `TreeItemContextMenuEventArgs` argument, which has the following properties:

@[template](/_contentTemplates/common/click-events.md#clickeventargs)
| Property | Type | Description |
| --- | --- | --- |
| `Item` | `object` | The data item that was right-clicked. |
| `ItemId` | `string` | The ID of the right-clicked node. |
| `MouseEventArgs` | `MouseEventArgs` | The mouse event args from the context menu event. |

## OnItemEdit

The `OnItemEdit` event fires when the user completes an inline edit on a tree node (requires `AllowEditing="true"`). Double-click or press F2 to start editing; Enter to confirm, Escape to cancel.

The event handler receives a `TreeItemEditEventArgs` argument:

| Property | Type | Description |
| --- | --- | --- |
| `ItemId` | `string` | The ID of the edited node. |
| `NewText` | `string` | The new text value after editing. |

@[template](/_contentTemplates/common/click-events.md#clickeventargs)
## OnItemDrop

The `OnItemDrop` event fires when a tree item is dropped onto another item during drag-and-drop (requires `EnableDragDrop="true"`).

The event handler receives `EventCallback<(string DraggedId, string TargetId)>`.

## SelectedItemsChanged

The `SelectedItemsChanged` event fires when the [selection](slug:treeview-selection-overview) is enabled and the user clicks on a new item. The callback type is `EventCallback<IEnumerable<string>>`.

## Example

>caption Handle Blazor TreeView Events

````RAZOR
<SunfishTreeView Data="@_flatData"
                IdField="Id"
                ParentIdField="ParentId"
                TextField="Name"
                CheckBoxMode="CheckBoxMode.Multiple"
                SelectionMode="TreeSelectionMode.Single"
                @bind-CheckedItems="_checkedIds"
                @bind-SelectedItems="_selectedIds"
                @bind-ExpandedItems="_expandedIds"
                OnItemClick="OnItemClicked"
                OnItemContextMenu="OnContextMenu"
                EnableDragDrop="true"
                OnItemDrop="OnDrop"
                AllowEditing="true"
                OnItemEdit="OnEdited" />

<p><strong>Selected:</strong> @(string.Join(", ", _selectedIds ?? []))</p>
<p><strong>Checked:</strong> @(string.Join(", ", _checkedIds ?? []))</p>
<p><strong>Log:</strong> @_log</p>

@code {
    record FlatNode(string Id, string? ParentId, string Name);

    List<object> _flatData = [
        new FlatNode("1", null, "Project"),
        new FlatNode("2", "1", "Design"),
        new FlatNode("3", "1", "Implementation"),
        new FlatNode("4", "2", "site.psd"),
        new FlatNode("5", "3", "index.js"),
    ];

    IEnumerable<string>? _checkedIds;
    IEnumerable<string>? _selectedIds;
    IEnumerable<string>? _expandedIds;
    string _log = "";

    void OnItemClicked(object item) =>
        _log = $"Clicked: {((FlatNode)item).Name}";

    void OnContextMenu(TreeItemContextMenuEventArgs args) =>
        _log = $"Context menu: {args.ItemId}";

    void OnDrop((string DraggedId, string TargetId) e) =>
        _log = $"Dropped {e.DraggedId} onto {e.TargetId}";

    void OnEdited(TreeItemEditEventArgs args) =>
        _log = $"Edited {args.ItemId} → {args.NewText}";
}
````

## See Also

  * [TreeView Overview](slug:treeview-overview)
  * [TreeView Selection](slug:treeview-selection-overview)
  * [TreeView CheckBoxes](slug:treeview-checkboxes-overview)
