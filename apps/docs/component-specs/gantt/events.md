---
title: Events
page_title: Gantt - Events
description: Events of the Gantt for Blazor
slug: gantt-events
tags: sunfish,blazor,gantt,events
published: True
position: 25
components: ["gantt"]
---
# Gantt Events

This article explains the events available in the Sunfish Gantt for Blazor. They are grouped logically.

* [CUD Event](#cud-events) - events related to Creating, Updating and Deleting items
* [OnTaskClick](#ontaskclick) - fires when the user clicks a task row
* [ViewChanged](#viewchanged) - fires when the active timeline view changes
* [OnExpand and OnCollapse](#onexpand-and-oncollapse) - events related to Expanding and Collapsing Gantt Tree items

## CUD Events

The `OnCreate`, `OnUpdate` and `OnDelete` events lets you get the data item that the user changed so you can transfer the user action to the actual data source.

The `OnTaskEdit` event lets you respond to user actions when they want to edit an item. For example, you can use it to prevent editing of certain items based on some condition.

You can read more about the CUD events in the [Gantt Tree Editing Overview](slug:gantt-tree-editing) article.

## OnTaskClick

The `OnTaskClick` event fires when the user clicks a task row in the tree list.

The event handler receives an argument of type `TItem` — the data item of the clicked row.

`OnTaskClick` — `EventCallback<TItem>`.

## ViewChanged

The `ViewChanged` event fires when the active timeline view changes (Day/Week/Month/Year). It is used as the callback half of `@bind-View`.

`ViewChanged` — `EventCallback<GanttView>`.

> `@bind-View` works because `View` and `ViewChanged` follow Blazor's standard two-way binding convention. If you only need to read the current view, use one-way binding with `View` and handle `ViewChanged` separately.

## TaskListWidthChanged

The `TaskListWidthChanged` event fires as a response to the user changing the width of the TreeList pane in the splitter.

## OnExpand and OnCollapse

The `OnExpand` and `OnCollapse` events fire as a response to the user expanding and collapsing an item of the Gantt Tree.

The event handlers receive arguments of type `GanttExpandEventArgs` and `GanttCollapseEventArgs` respectively which expose the following fields:

* `Item` — `object`. Cast to your model class to obtain the current data item.
* `ShouldRender` — `bool`, default `true`. Set to `false` in the handler to suppress the re-render after the expand/collapse action.

### OnCreate ParentItem

The `OnCreate` event uses `GanttCreateEventArgs` which has the following fields:

* `Item` — `object`. The new item being created.
* `ParentItem` — `object?`. The parent item under which the new item is created, or `null` when creating a root-level item.


>caption Handle OnExpand and OnCollapse events
````RAZOR
@eventActions

<SunfishGantt Data="@Data"
              Width="1200px"
              Height="600px"
              IdField="Id"
              ParentIdField="ParentId"
              OnExpand="@OnItemExpand"
              OnCollapse="@OnItemCollapse"
              OnUpdate="@UpdateItem"
              OnDelete="@DeleteItem"
              OnCreate="@CreateItem">
    <GanttToolBarTemplate>
        <GanttCommandButton Command="Add" Icon="@SvgIcon.Plus">Add</GanttCommandButton>
    </GanttToolBarTemplate>
    <GanttViews>
        <GanttDayView></GanttDayView>
        <GanttWeekView></GanttWeekView>
        <GanttMonthView></GanttMonthView>
        <GanttYearView></GanttYearView>
    </GanttViews>
    <GanttColumns>
        <GanttColumn Field="Id"
                     Visible="false">
        </GanttColumn>
        <GanttColumn Field="Title"
                     Expandable="true"
                     Width="160px"
                     Title="Task Title">
        </GanttColumn>
        <GanttColumn Field="PercentComplete"
                     Width="60px">
        </GanttColumn>
        <GanttColumn Field="Start"
                     Width="100px"
                     TextAlign="@ColumnTextAlign.Right">
        </GanttColumn>
        <GanttColumn Field="End"
                     DisplayFormat="End: {0:d}"
                     Width="100px">
        </GanttColumn>
        <GanttCommandColumn>
            <GanttCommandButton Command="Add" Icon="@SvgIcon.Plus"></GanttCommandButton>
            <GanttCommandButton Command="Delete" Icon="@SvgIcon.Trash"></GanttCommandButton>
        </GanttCommandColumn>
    </GanttColumns>
</SunfishGantt>

@code {
    public DateTime SelectedDate { get; set; } = new DateTime(2019, 11, 11, 6, 0, 0);

    public string eventActions { get; set; }

    class FlatModel
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Title { get; set; }
        public double PercentComplete { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public int LastId { get; set; } = 1;
    List<FlatModel> Data { get; set; }

    protected override void OnInitialized()
    {
        Data = new List<FlatModel>();
        var random = new Random();

        for (int i = 1; i < 6; i++)
        {
            var newItem = new FlatModel()
                {
                    Id = LastId,
                    Title = "Employee  " + i.ToString(),
                    Start = new DateTime(2020, 12, 6 + i),
                    End = new DateTime(2020, 12, 11 + i),
                    PercentComplete = Math.Round(random.NextDouble(), 2)
                };

            Data.Add(newItem);
            var parentId = LastId;
            LastId++;

            for (int j = 0; j < 5; j++)
            {
                Data.Add(new FlatModel()
                    {
                        Id = LastId,
                        ParentId = parentId,
                        Title = "    Employee " + i + " : " + j.ToString(),
                        Start = new DateTime(2020, 12, 6 + i + j),
                        End = new DateTime(2020, 12, 7 + i + j),
                        PercentComplete = Math.Round(random.NextDouble(), 2)
                    });

                LastId++;
            }
        }

        base.OnInitialized();
    }

    private async Task CreateItem(GanttCreateEventArgs args)
    {
        var argsItem = args.Item as FlatModel;

        argsItem.Id = LastId++;

        if (args.ParentItem != null)
        {
            var parent = (FlatModel)args.ParentItem;

            argsItem.ParentId = parent.Id;
        }

        Data.Insert(0, argsItem);

        CalculateParentPercentRecursive(argsItem);
        CalculateParentRangeRecursive(argsItem);
    }

    private async Task UpdateItem(GanttUpdateEventArgs args)
    {
        var item = args.Item as FlatModel;

        var foundItem = Data.FirstOrDefault(i => i.Id.Equals(item.Id));

        if (foundItem != null)
        {
            var startOffset = item.Start - foundItem.Start;
            if (startOffset != TimeSpan.Zero)
            {
                MoveChildrenRecursive(foundItem, startOffset);
            }

            foundItem.Title = item.Title;
            foundItem.Start = item.Start;
            foundItem.End = item.End;
            foundItem.PercentComplete = item.PercentComplete;
        }

        CalculateParentPercentRecursive(foundItem);
        CalculateParentRangeRecursive(foundItem);
    }

    private async Task DeleteItem(GanttDeleteEventArgs args)
    {
        var item = Data.FirstOrDefault(i => i.Id.Equals((args.Item as FlatModel).Id));

        RemoveChildRecursive(item);

        CalculateParentPercentRecursive(item);
        CalculateParentRangeRecursive(item);
    }

    public void OnItemExpand(GanttExpandEventArgs args)
    {
        var item = args.Item as FlatModel;
        eventActions = $"The user expanded {item.Title} with ID {item.Id}";
    }

    public void OnItemCollapse(GanttCollapseEventArgs args)
    {
        var item = args.Item as FlatModel;
        eventActions = $"The user collapsed {item.Title} with ID {item.Id}";
    }

    private void RemoveChildRecursive(FlatModel item)
    {
        var children = GetChildren(item).ToList();

        foreach (var child in children)
        {
            RemoveChildRecursive(child);
        }

        Data.Remove(item);
    }

    private void CalculateParentPercentRecursive(FlatModel item)
    {
        if (item.ParentId != null)
        {
            var parent = GetParent(item);

            var children = GetChildren(parent);

            if (children.Any())
            {
                parent.PercentComplete = children.Average(i => i.PercentComplete);

                CalculateParentPercentRecursive(parent);
            }
        }
    }

    private void CalculateParentRangeRecursive(FlatModel item)
    {
        if (item.ParentId != null)
        {
            var parent = GetParent(item);

            var children = GetChildren(parent);

            if (children.Any())
            {
                parent.Start = children.Min(i => i.Start);
                parent.End = children.Max(i => i.End);

                CalculateParentRangeRecursive(parent);
            }
        }
    }

    private void MoveChildrenRecursive(FlatModel item, TimeSpan offset)
    {
        var children = GetChildren(item);

        foreach (var child in children)
        {
            child.Start = child.Start.Add(offset);
            child.End = child.End.Add(offset);

            MoveChildrenRecursive(child, offset);
        }
    }

    private FlatModel GetParent(FlatModel item)
    {
        return Data.FirstOrDefault(i => i.Id.Equals(item.ParentId));
    }

    private IEnumerable<FlatModel> GetChildren(FlatModel item)
    {
        return Data.Where(i => item.Id.Equals(i.ParentId));
    }
}
````

## See Also

  * [Live Demo: Gantt Events](https://demos.sunfish.dev/blazor-ui/gantt/events)