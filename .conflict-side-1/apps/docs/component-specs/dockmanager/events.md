---
title: Events
page_title: DockManager - Events
description: Events in the DockManager for Blazor.
slug: dockmanager-events
tags: sunfish,blazor,dockmanager,events
published: true
position: 20
components: ["dockmanager"]
---
# DockManager Events

This article explains the events available in the Sunfish DockManager for Blazor:

* [OnPaneClosed](#onpaneclosed)
* [OnPaneFloat](#onpanefloat)
* [OnPanePin](#onpanepin)
* [OnPaneActivated](#onpaneactivated)
* [OnTabReordered](#ontabreordered)
* [OnPaneMoved](#onpanemoved)
* [OnLayoutChanged](#onlayoutchanged)

## OnPaneClosed

The OnPaneClosed event fires when a pane is closed (removed from the dock manager). The event handler receives the pane Id as a string.

## OnPaneFloat

The OnPaneFloat event fires when a pane is toggled between docked and floating states. The event handler receives the pane Id as a string.

## OnPanePin

The OnPanePin event fires when the pin button of a pane is clicked. The event handler receives the pane Id as a string.

## OnPaneActivated

The OnPaneActivated event fires when a pane becomes the active tab (via user click). The event handler receives the pane Id as a string.

## OnTabReordered

The OnTabReordered event fires when a tab is reordered within the tab strip via drag-and-drop.

The event handler receives a DockTabReorderEventArgs object that contains:

| Property | Type | Description |
|---|---|---|
| TabGroupId | string | The Id of the tab group in which the reorder occurred. |
| PaneId | string | The Id of the pane (tab) that was moved. |
| OldIndex | int | The original index of the tab before the drag. |
| NewIndex | int | The new index of the tab after the drop. |

## OnPaneMoved

The OnPaneMoved event fires when a tab is moved from one tab group to another via drag-and-drop.

The event handler receives a DockPaneMoveEventArgs object that contains:

| Property | Type | Description |
|---|---|---|
| PaneId | string | The Id of the pane (tab) that was moved. |
| SourceGroupId | string | The Id of the source tab group from which the pane was moved. |
| TargetGroupId | string | The Id of the target tab group to which the pane was moved. |

## OnLayoutChanged

The OnLayoutChanged event fires whenever the layout changes. This includes pane registration, removal, reordering, floating, docking, and cross-pane tab moves. Use this event to persist layout state or trigger dependent UI updates.

The event handler receives no arguments (EventCallback).

## Example

>caption DockManager with all available events.

`````RAZOR
<SunfishDockManager @ref="@DockManagerRef"
                    Height="70vh"
                    Width="90vw"
                    OnDock="@OnPaneDock"
                    OnUndock="@OnPaneUndock"
                    OnPin="@OnPanePin"
                    OnPaneResize="@OnPaneResize"
                    OnUnpin="@OnPaneUnpin">
    <DockManagerPanes>

        <DockManagerSplitPane Orientation="@DockManagerPaneOrientation.Vertical"
                              Size="40%"
                              Id="SplitPane">
            <Panes>

                <DockManagerContentPane HeaderText="Pane 1"
                                        Id="Pane1"
                                        Size="50%"
                                        Unpinned="@Pane1Unpinned"
                                        UnpinnedChanged="@Pane1UnpinnedChanged"
                                        UnpinnedSize="@Pane1UnpinnedSize"
                                        UnpinnedSizeChanged="@Pane1UnpinnedSizeChanged"
                                        Closeable="false">
                    <Content>
                        Pane 1. Undocking is allowed. Docking over it is cancelled.
                        <code>UnpinnedChanged</code> and <code>UnpinnedSizeChanged</code> are handled.
                        Current
                        <code>UnpinnedSize</code>:
                        <strong>@Pane1UnpinnedSize</strong>
                    </Content>
                </DockManagerContentPane>

                <DockManagerContentPane HeaderText="Pane 2"
                                        Id="Pane2"
                                        Size="50%"
                                        Closeable="false">
                    <Content>
                        Pane 2. Docking over it is allowed. Undocking is cancelled.
                        <br />
                        <SunfishButton ThemeColor="@ThemeConstants.Button.ThemeColor.Primary"
                                       Enabled="@( !Pane4Visible || !FloatingPaneVisible )"
                                       OnClick="@( () => { Pane4Visible = true; FloatingPaneVisible = true; DockManagerRef?.Refresh(); })">
                            Restore Closed Panes
                        </SunfishButton>
                    </Content>
                </DockManagerContentPane>

            </Panes>
        </DockManagerSplitPane>

        <DockManagerTabGroupPane Id="TabGroupPane">
            <Panes>

                <DockManagerContentPane HeaderText="Pane 3"
                                        Id="Pane3"
                                        Closeable="false">
                    <Content>
                        Pane 3. Unpinning is possible, but pinning is cancelled.
                    </Content>
                </DockManagerContentPane>

                <DockManagerContentPane HeaderText="Pane 4"
                                        Id="Pane4"
                                        Visible="@Pane4Visible"
                                        VisibleChanged="OnPane4VisibleChanged">
                    <Content>
                        Pane 4. Can be closed. Unpinning is cancelled.
                    </Content>
                </DockManagerContentPane>

            </Panes>
        </DockManagerTabGroupPane>
    </DockManagerPanes>

    <DockManagerFloatingPanes>
        <DockManagerSplitPane Id="FloatingSplitPane">
            <Panes>

                <DockManagerContentPane HeaderText="Floating Pane"
                                        Id="FloatingPane"
                                        Visible="@FloatingPaneVisible"
                                        VisibleChanged="OnFloatingPaneVisibleChanged">
                    <Content>
                        Floating Pane. Can be closed.
                    </Content>
                </DockManagerContentPane>

            </Panes>
        </DockManagerSplitPane>
    </DockManagerFloatingPanes>
</SunfishDockManager>

<p style="color: var(--kendo-color-primary)">DockManager Events (latest on top):</p>

<div style="height: 20vh; border:1px solid var(--kendo-color-border); overflow: auto;">
    @foreach (var item in DockManagetEventLog)
    {
        <div>@( (MarkupString)item )</div>
    }
</div>

@code {
    private SunfishDockManager? DockManagerRef { get; set; }

    private bool Pane1Unpinned { get; set; }
    private string Pane1UnpinnedSize { get; set; } = "360px";
    private bool Pane4Visible { get; set; } = true;
    private bool FloatingPaneVisible { get; set; } = true;

    private List<string> DockManagetEventLog { get; set; } = new List<string>();

    private void OnPaneDock(DockManagerDockEventArgs args)
    {
        if (args.TargetPaneId == "Pane1")
        {
            args.IsCancelled = true;
            DockManagetEventLog.Insert(0, $"Pane <strong>{args.PaneId}</strong> was about to dock to pane <strong>{args.TargetPaneId}</strong>. Event cancelled.");
        }
        else
        {
            DockManagetEventLog.Insert(0, $"Pane <strong>{args.PaneId}</strong> was docked to pane <strong>{args.TargetPaneId}.");
        }
    }

    private void OnPaneUndock(DockManagerUndockEventArgs args)
    {
        if (args.PaneId == "Pane2")
        {
            args.IsCancelled = true;
            DockManagetEventLog.Insert(0, $"Pane <strong>{args.PaneId}</strong> was about to undock. Event cancelled.");
        }
        else
        {
            DockManagetEventLog.Insert(0, $"Pane <strong>{args.PaneId}</strong> was undocked.");
        }
    }

    private void OnPanePin(DockManagerPinEventArgs args)
    {
        if (args.PaneId == "Pane3")
        {
            args.IsCancelled = true;
            DockManagetEventLog.Insert(0, $"Pane <strong>{args.PaneId}</strong> was about to pin. Event cancelled.");
        }
        else
        {
            DockManagetEventLog.Insert(0, $"[DockManager OnPanePin] Pane <strong>{args.PaneId}</strong> was pinned.");
        }
    }

    private void OnPaneResize(DockManagerPaneResizeEventArgs args)
    {
        DockManagetEventLog.Insert(0, $"Pane <strong>{args.PaneId}</strong> was resized to {args.Size}.");
    }

    private void Pane1UnpinnedChanged(bool newUnpinned)
    {
        Pane1Unpinned = newUnpinned;

        DockManagetEventLog.Insert(0, $"[Pane UnpinnedChanged] Pane <strong>Pane 1</strong> was {(newUnpinned ? "unpinned" : "pinned")}.");
    }

    private void Pane1UnpinnedSizeChanged(string newUnpinnedSize)
    {
        Pane1UnpinnedSize = newUnpinnedSize;

        DockManagetEventLog.Insert(0, $"Pane <strong>Pane 1</strong> was resized to {newUnpinnedSize} while unpinned.");
    }

    private void OnPaneUnpin(DockManagerUnpinEventArgs args)
    {
        if (args.PaneId == "Pane4")
        {
            args.IsCancelled = true;
            DockManagetEventLog.Insert(0, $"Pane <strong>{args.PaneId}</strong> was about to unpin. Event cancelled.");
        }
        else
        {
            DockManagetEventLog.Insert(0, $"[DockManager OnUnpin] Pane <strong>{args.PaneId}</strong> was unpinned.");
        }
    }

    private void OnPane4VisibleChanged(bool newVisible)
    {
        Pane4Visible = newVisible;

        DockManagetEventLog.Insert(0, $"Pane <strong>Pane4</strong> was closed.");
    }

    private void OnFloatingPaneVisibleChanged(bool newVisible)
    {
        FloatingPaneVisible = newVisible;

        DockManagetEventLog.Insert(0, $"Pane <strong>FloatingPane</strong> was closed.");
    }
}
`````

## Next Steps

* [Manage the Dock Manager state](slug:dockmanager-state).


## See Also

* [DockManager Overview](slug:dockmanager-overview)
