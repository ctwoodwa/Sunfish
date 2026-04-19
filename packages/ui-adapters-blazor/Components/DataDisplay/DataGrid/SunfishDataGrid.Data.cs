using Sunfish.Foundation.Data;
using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Data processing pipeline for SunfishDataGrid: filtering, sorting, grouping, paging.
/// </summary>
public partial class SunfishDataGrid<TItem>
{
    // ── Data Processing ─────────────────────────────────────────────────

    private CancellationTokenSource? _currentCts;

    internal async Task ProcessDataAsync()
    {
        // Cancel previous request
        _currentCts?.Cancel();
        _currentCts = new CancellationTokenSource();
        var token = _currentCts.Token;

        if (OnRead.HasDelegate)
        {
            var args = new GridReadEventArgs<TItem> { Request = GetState(), CancellationToken = token };
            await OnRead.InvokeAsync(args);
            if (token.IsCancellationRequested) return;
            _displayedItems = args.Data.ToList();
            _state.TotalCount = args.Total;
        }
        else
        {
            ProcessDataClientSide();
        }
    }

    private void ProcessDataClientSide()
    {
        if (Data is null)
        {
            _displayedItems = [];
            _groupedRows = [];
            _state.TotalCount = 0;
            return;
        }

        IEnumerable<TItem> items = Data;

        // Apply global search
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            items = ApplySearch(items, _searchText);
        }

        // Apply filters
        foreach (var filter in _state.FilterDescriptors)
        {
            items = ApplyFilter(items, filter);
        }

        // Apply composite filters
        foreach (var composite in _state.CompositeFilterDescriptors)
        {
            items = ApplyCompositeFilter(items, composite);
        }

        // Apply sorting
        items = ApplySort(items);

        var allItems = items.ToList();
        _state.TotalCount = allItems.Count;

        // Apply grouping
        if (_state.GroupDescriptors.Count > 0)
        {
            _groupedRows = BuildGroups(allItems, _state.GroupDescriptors, 0);

            // For grouped data, build a flat display list respecting collapsed state
            _displayedItems = FlattenGroups(_groupedRows);

            // Paging on grouped data applies to the flattened visible items
            if (Pageable && _state.PageSize > 0)
            {
                // TotalCount should reflect total data items, not group rows
                _state.TotalCount = allItems.Count;
            }
        }
        else
        {
            _groupedRows = [];

            // Apply paging
            if (Pageable && _state.PageSize > 0)
            {
                var skip = (_state.CurrentPage - 1) * _state.PageSize;
                _displayedItems = allItems.Skip(skip).Take(_state.PageSize).ToList();
            }
            else
            {
                _displayedItems = allItems;
            }
        }
    }

    // ── Grouping ────────────────────────────────────────────────────────

    private List<GridGroupRow<TItem>> BuildGroups(
        List<TItem> items,
        List<GroupDescriptor> descriptors,
        int depth)
    {
        if (depth >= descriptors.Count)
            return [];

        var descriptor = descriptors[depth];
        var prop = typeof(TItem).GetProperty(descriptor.Field);
        if (prop is null) return [];

        var grouped = items.GroupBy(item => prop.GetValue(item));

        // Sort groups by key
        var orderedGroups = descriptor.Direction == SortDirection.Ascending
            ? grouped.OrderBy(g => g.Key)
            : grouped.OrderByDescending(g => g.Key);

        var result = new List<GridGroupRow<TItem>>();
        foreach (var group in orderedGroups)
        {
            var groupItems = group.ToList();
            var groupRow = new GridGroupRow<TItem>
            {
                Field = descriptor.Field,
                Key = group.Key,
                Items = groupItems,
                Depth = depth,
                ChildGroups = depth + 1 < descriptors.Count
                    ? BuildGroups(groupItems, descriptors, depth + 1)
                    : []
            };
            result.Add(groupRow);
        }

        return result;
    }

    private List<TItem> FlattenGroups(List<GridGroupRow<TItem>> groups)
    {
        var result = new List<TItem>();
        foreach (var group in groups)
        {
            if (_collapsedGroups.Contains(group.GroupKey))
                continue; // Group is collapsed, skip its items

            if (group.HasChildGroups)
            {
                result.AddRange(FlattenGroups(group.ChildGroups));
            }
            else
            {
                result.AddRange(group.Items);
            }
        }
        return result;
    }

    /// <summary>Toggles a group's collapsed state.</summary>
    internal async Task ToggleGroup(string groupKey)
    {
        if (!_collapsedGroups.Remove(groupKey))
            _collapsedGroups.Add(groupKey);

        // Rebuild the flat display list
        if (_state.GroupDescriptors.Count > 0)
        {
            _displayedItems = FlattenGroups(_groupedRows);
        }

        await NotifyStateChanged("Group");
        StateHasChanged();
    }

    /// <summary>Adds a group descriptor and reprocesses data.</summary>
    public async Task GroupBy(string field, SortDirection direction = SortDirection.Ascending)
    {
        if (!Groupable) return;
        // Respect per-column Groupable setting
        var column = _visibleColumns.FirstOrDefault(c => c.Field == field);
        if (column != null && !column.Groupable) return;
        if (_state.GroupDescriptors.Any(g => g.Field == field)) return;

        _state.GroupDescriptors.Add(new GroupDescriptor { Field = field, Direction = direction });
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyStateChanged("Group");
        StateHasChanged();
    }

    /// <summary>Removes a group descriptor and reprocesses data.</summary>
    public async Task Ungroup(string field)
    {
        var existing = _state.GroupDescriptors.FirstOrDefault(g => g.Field == field);
        if (existing is null) return;

        _state.GroupDescriptors.Remove(existing);
        _collapsedGroups.RemoveWhere(k => k.StartsWith($"{field}:"));
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyStateChanged("Group");
        StateHasChanged();
    }

    /// <summary>Removes all group descriptors.</summary>
    public async Task UngroupAll()
    {
        _state.GroupDescriptors.Clear();
        _collapsedGroups.Clear();
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyStateChanged("Group");
        StateHasChanged();
    }

    /// <summary>Whether the given group key is collapsed.</summary>
    internal bool IsGroupCollapsed(string groupKey) => _collapsedGroups.Contains(groupKey);

    // ── Search ──────────────────────────────────────────────────────────

    private IEnumerable<TItem> ApplySearch(IEnumerable<TItem> items, string searchText)
    {
        var lower = searchText.ToLowerInvariant();
        var props = _visibleColumns
            .Where(c => !string.IsNullOrEmpty(c.Field))
            .Select(c => typeof(TItem).GetProperty(c.Field))
            .Where(p => p is not null)
            .ToList();

        return items.Where(item =>
            props.Any(prop =>
                prop!.GetValue(item)?.ToString()?.Contains(lower, StringComparison.OrdinalIgnoreCase) == true));
    }

    internal void OnSearchChanged(ChangeEventArgs e)
    {
        _searchText = e.Value?.ToString() ?? "";

        // Cancel any pending debounce
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        // Debounce: wait SearchDelay ms before applying the filter
        _ = Task.Delay(SearchDelay, token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;
            await InvokeAsync(async () =>
            {
                _state.CurrentPage = 1;
                await ProcessDataAsync();
                await NotifyStateChanged("Search");
                StateHasChanged();
            });
        }, TaskScheduler.Default);
    }

    // ── CSV Export ──────────────────────────────────────────────────────

    /// <summary>
    /// Generates CSV content from grid data. When ExportAllPages is true (default),
    /// exports all filtered/sorted data. When false, exports only the current page.
    /// Fires OnBeforeExport and OnAfterExport lifecycle events.
    /// </summary>
    public async Task<string> ExportToCsvAsync(bool includeHeaders = true, string separator = ",")
    {
        // Fire OnBeforeExport
        var beforeArgs = new GridExportEventArgs { Format = "csv" };
        if (OnBeforeExport.HasDelegate)
        {
            await OnBeforeExport.InvokeAsync(beforeArgs);
            if (beforeArgs.IsCancelled) return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        var columns = _visibleColumns;

        if (includeHeaders)
        {
            sb.AppendLine(string.Join(separator, columns.Select(c => EscapeCsv(c.DisplayTitle, separator))));
        }

        IEnumerable<TItem> items;
        if (ExportAllPages && Data is not null)
        {
            items = Data;
            if (!string.IsNullOrWhiteSpace(_searchText))
                items = ApplySearch(items, _searchText);
            foreach (var filter in _state.FilterDescriptors)
                items = ApplyFilter(items, filter);
            items = ApplySort(items);
        }
        else
        {
            items = _displayedItems;
        }

        var itemList = items.ToList();
        foreach (var item in itemList)
        {
            var values = columns.Select(c => EscapeCsv(c.GetDisplayValue(item), separator));
            sb.AppendLine(string.Join(separator, values));
        }

        var result = sb.ToString();

        // Fire OnAfterExport
        if (OnAfterExport.HasDelegate)
        {
            var afterArgs = new GridExportEventArgs
            {
                Format = "csv",
                Data = result,
                RowCount = itemList.Count
            };
            await OnAfterExport.InvokeAsync(afterArgs);
        }

        return result;
    }

    /// <summary>
    /// Synchronous CSV export (backward compatibility). Does not fire lifecycle events.
    /// Use ExportToCsvAsync for full lifecycle support.
    /// </summary>
    public string ExportToCsv(bool includeHeaders = true, string separator = ",")
    {
        var sb = new System.Text.StringBuilder();
        var columns = _visibleColumns;

        if (includeHeaders)
        {
            sb.AppendLine(string.Join(separator, columns.Select(c => EscapeCsv(c.DisplayTitle, separator))));
        }

        IEnumerable<TItem> items;
        if (ExportAllPages && Data is not null)
        {
            items = Data;
            if (!string.IsNullOrWhiteSpace(_searchText))
                items = ApplySearch(items, _searchText);
            foreach (var filter in _state.FilterDescriptors)
                items = ApplyFilter(items, filter);
            items = ApplySort(items);
        }
        else
        {
            items = _displayedItems;
        }

        foreach (var item in items)
        {
            var values = columns.Select(c => EscapeCsv(c.GetDisplayValue(item), separator));
            sb.AppendLine(string.Join(separator, values));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value, string separator)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    // ── Excel Export ────────────────────────────────────────────────────

    /// <summary>
    /// Generates an <c>.xlsx</c> workbook from visible columns and current data, then
    /// triggers a browser file download.
    /// </summary>
    /// <param name="options">
    /// Optional export options.  When <c>null</c>, defaults are used (all pages if the
    /// grid's own <c>ExportAllPages</c> parameter is set, headers included,
    /// sheet named <c>"Export"</c>, first row frozen, columns auto-fitted).
    /// </param>
    /// <remarks>
    /// Fires <see cref="OnBeforeExport"/> before generating bytes (set
    /// <see cref="GridExportEventArgs.IsCancelled"/> to abort) and
    /// <see cref="OnAfterExport"/> after the download is queued.
    /// </remarks>
    public async Task ExportToExcelAsync(XlsxExportOptions? options = null)
    {
        options ??= new XlsxExportOptions { ExportAllPages = ExportAllPages };

        // Fire OnBeforeExport
        var beforeArgs = new GridExportEventArgs { Format = "xlsx" };
        if (OnBeforeExport.HasDelegate)
        {
            await OnBeforeExport.InvokeAsync(beforeArgs);
            if (beforeArgs.IsCancelled) return;
        }

        // Build column descriptors from visible columns
        var columns = _visibleColumns
            .Select(c => new ExportColumnDescriptor(c.Field, c.DisplayTitle, c.Format))
            .ToList();

        // Resolve data rows
        IEnumerable<TItem> items;
        if (options.ExportAllPages && Data is not null)
        {
            items = Data;
            if (!string.IsNullOrWhiteSpace(_searchText))
                items = ApplySearch(items, _searchText);
            foreach (var filter in _state.FilterDescriptors)
                items = ApplyFilter(items, filter);
            items = ApplySort(items);
        }
        else
        {
            items = _displayedItems;
        }

        var itemList = items.ToList();

        // Generate XLSX bytes (pure C# — no Blazor dependency)
        var bytes = XlsxExportWriter.Write(columns, itemList, options);

        // Trigger browser download
        await TriggerXlsxDownloadAsync(bytes, options);

        // Fire OnAfterExport
        if (OnAfterExport.HasDelegate)
        {
            await OnAfterExport.InvokeAsync(new GridExportEventArgs
            {
                Format = "xlsx",
                RowCount = itemList.Count
            });
        }
    }

    /// <summary>
    /// Convenience overload that accepts a file name and export-all-pages flag directly.
    /// </summary>
    /// <param name="fileName">
    /// File name for the download (e.g. <c>"report.xlsx"</c>).
    /// When <c>null</c> a timestamped default is used.
    /// </param>
    /// <param name="exportAllPages">
    /// When <c>true</c>, all filtered/sorted rows are exported regardless of pagination.
    /// </param>
    public Task ExportToExcelAsync(string? fileName, bool exportAllPages = false)
        => ExportToExcelAsync(new XlsxExportOptions
        {
            FileName = fileName,
            ExportAllPages = exportAllPages
        });

    private async Task TriggerXlsxDownloadAsync(byte[] bytes, XlsxExportOptions options)
    {
        var fileName = string.IsNullOrWhiteSpace(options.FileName)
            ? $"grid-export-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx"
            : options.FileName;

        const string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // Transport bytes as base64 through the existing IDownloadService / JS `download`
        // function in sunfish-clipboard-download.js.  Base64 encoding is the standard
        // Blazor pattern for binary payloads; it avoids the overhead of raw byte-array
        // marshalling through JS interop for typical file sizes.
        await _downloadService.DownloadAsync(new Internal.Interop.DownloadRequest
        {
            FileName = fileName,
            ContentType = mimeType,
            Base64Content = Convert.ToBase64String(bytes)
        });
    }

    // ── Filtering (extended operators) ──────────────────────────────────

    private static IEnumerable<TItem> ApplyFilter(IEnumerable<TItem> items, FilterDescriptor filter)
    {
        if (string.IsNullOrEmpty(filter.Field)) return items;

        var prop = typeof(TItem).GetProperty(filter.Field);
        if (prop is null) return items;

        // Null check operators don't need a filter value
        if (filter.Operator == FilterOperator.IsNull)
            return items.Where(item => prop.GetValue(item) is null);

        if (filter.Operator == FilterOperator.IsNotNull)
            return items.Where(item => prop.GetValue(item) is not null);

        if (filter.Value is null) return items;

        var filterValue = filter.Value.ToString()?.ToLowerInvariant() ?? "";

        return filter.Operator switch
        {
            FilterOperator.Contains => items.Where(item =>
                prop.GetValue(item)?.ToString()?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) == true),

            FilterOperator.Equals => items.Where(item =>
                string.Equals(prop.GetValue(item)?.ToString(), filterValue, StringComparison.OrdinalIgnoreCase)),

            FilterOperator.NotEquals => items.Where(item =>
                !string.Equals(prop.GetValue(item)?.ToString(), filterValue, StringComparison.OrdinalIgnoreCase)),

            FilterOperator.StartsWith => items.Where(item =>
                prop.GetValue(item)?.ToString()?.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase) == true),

            FilterOperator.EndsWith => items.Where(item =>
                prop.GetValue(item)?.ToString()?.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase) == true),

            FilterOperator.GreaterThan => ApplyComparison(items, prop, filterValue, (cmp) => cmp > 0),
            FilterOperator.GreaterThanOrEqual => ApplyComparison(items, prop, filterValue, (cmp) => cmp >= 0),
            FilterOperator.LessThan => ApplyComparison(items, prop, filterValue, (cmp) => cmp < 0),
            FilterOperator.LessThanOrEqual => ApplyComparison(items, prop, filterValue, (cmp) => cmp <= 0),

            _ => items
        };
    }

    private static IEnumerable<TItem> ApplyComparison(
        IEnumerable<TItem> items,
        System.Reflection.PropertyInfo prop,
        string filterValue,
        Func<int, bool> comparison)
    {
        return items.Where(item =>
        {
            var value = prop.GetValue(item);
            if (value is null) return false;

            if (value is IComparable comparable)
            {
                // Try to convert filter value to the property type for proper comparison
                try
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    var convertedFilter = Convert.ChangeType(filterValue, targetType);
                    return comparison(comparable.CompareTo(convertedFilter));
                }
                catch
                {
                    // Fall back to string comparison
                    return comparison(string.Compare(value.ToString(), filterValue, StringComparison.OrdinalIgnoreCase));
                }
            }

            return false;
        });
    }

    private static IEnumerable<TItem> ApplyCompositeFilter(IEnumerable<TItem> items, CompositeFilterDescriptor composite)
    {
        if (composite.Filters.Count == 0) return items;

        return composite.LogicalOperator == FilterCompositionOperator.And
            ? items.Where(item => composite.Filters.All(f => MatchesFilter(item, f)))
            : items.Where(item => composite.Filters.Any(f => MatchesFilter(item, f)));
    }

    private static bool MatchesFilter(TItem item, FilterDescriptor filter)
    {
        if (string.IsNullOrEmpty(filter.Field)) return true;
        var prop = typeof(TItem).GetProperty(filter.Field);
        if (prop is null) return true;

        if (filter.Operator == FilterOperator.IsNull)
            return prop.GetValue(item) is null;
        if (filter.Operator == FilterOperator.IsNotNull)
            return prop.GetValue(item) is not null;
        if (filter.Value is null) return true;

        var filterValue = filter.Value.ToString()?.ToLowerInvariant() ?? "";
        var propValue = prop.GetValue(item);

        return filter.Operator switch
        {
            FilterOperator.Contains => propValue?.ToString()?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) == true,
            FilterOperator.Equals => string.Equals(propValue?.ToString(), filterValue, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => !string.Equals(propValue?.ToString(), filterValue, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => propValue?.ToString()?.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase) == true,
            FilterOperator.EndsWith => propValue?.ToString()?.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase) == true,
            _ => true
        };
    }

    // ── Sorting ─────────────────────────────────────────────────────────

    private IEnumerable<TItem> ApplySort(IEnumerable<TItem> items)
    {
        if (_state.SortDescriptors.Count == 0) return items;

        IOrderedEnumerable<TItem>? ordered = null;
        foreach (var sort in _state.SortDescriptors)
        {
            var prop = typeof(TItem).GetProperty(sort.Field);
            if (prop is null) continue;

            if (ordered is null)
            {
                ordered = sort.Direction == SortDirection.Ascending
                    ? items.OrderBy(item => prop.GetValue(item))
                    : items.OrderByDescending(item => prop.GetValue(item));
            }
            else
            {
                ordered = sort.Direction == SortDirection.Ascending
                    ? ordered.ThenBy(item => prop.GetValue(item))
                    : ordered.ThenByDescending(item => prop.GetValue(item));
            }
        }

        return ordered ?? items;
    }

    // ── Event Handlers: Sorting ─────────────────────────────────────────

    internal async Task OnHeaderClick(SunfishGridColumn<TItem> column, bool isSortable, MouseEventArgs? e = null)
    {
        if (!isSortable) return;

        var isMultiSort = SortMode == GridSortMode.Multiple && (e?.CtrlKey == true || e?.MetaKey == true);
        var existing = _state.SortDescriptors.FirstOrDefault(s => s.Field == column.Field);

        if (existing is null)
        {
            if (!isMultiSort)
                _state.SortDescriptors.Clear();
            _state.SortDescriptors.Add(new SortDescriptor
            {
                Field = column.Field,
                Direction = SortDirection.Ascending
            });
        }
        else if (existing.Direction == SortDirection.Ascending)
        {
            existing.Direction = SortDirection.Descending;
        }
        else
        {
            _state.SortDescriptors.Remove(existing);
        }

        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Sort");
    }

    // ── Event Handlers: Filtering ───────────────────────────────────────

    internal async Task OnFilterChanged(string field, ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        var existing = _state.FilterDescriptors.FirstOrDefault(f => f.Field == field);

        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing != null)
                _state.FilterDescriptors.Remove(existing);
        }
        else
        {
            if (existing != null)
            {
                existing.Value = value;
            }
            else
            {
                _state.FilterDescriptors.Add(new FilterDescriptor
                {
                    Field = field,
                    Operator = FilterOperator.Contains,
                    Value = value
                });
            }
        }

        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
    }

    // ── FilterMenu support ──────────────────────────────────────────────

    internal void ToggleFilterMenu(string field)
    {
        if (_filterMenuField == field)
        {
            _filterMenuField = null;
        }
        else
        {
            _filterMenuField = field;
            var existing = _state.FilterDescriptors.FirstOrDefault(f => f.Field == field);
            _filterMenuOperator = existing?.Operator ?? FilterOperator.Contains;
            _filterMenuValue = existing?.Value?.ToString() ?? "";
        }
    }

    internal async Task ApplyFilterMenu()
    {
        if (_filterMenuField == null) return;

        var existing = _state.FilterDescriptors.FirstOrDefault(f => f.Field == _filterMenuField);

        if (string.IsNullOrWhiteSpace(_filterMenuValue)
            && _filterMenuOperator != FilterOperator.IsNull
            && _filterMenuOperator != FilterOperator.IsNotNull)
        {
            if (existing != null)
                _state.FilterDescriptors.Remove(existing);
        }
        else
        {
            if (existing != null)
            {
                existing.Operator = _filterMenuOperator;
                existing.Value = _filterMenuValue;
            }
            else
            {
                _state.FilterDescriptors.Add(new FilterDescriptor
                {
                    Field = _filterMenuField,
                    Operator = _filterMenuOperator,
                    Value = _filterMenuValue
                });
            }
        }

        _filterMenuField = null;
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
    }

    internal async Task ClearFilterMenu()
    {
        if (_filterMenuField == null) return;
        var existing = _state.FilterDescriptors.FirstOrDefault(f => f.Field == _filterMenuField);
        if (existing != null)
            _state.FilterDescriptors.Remove(existing);

        _filterMenuField = null;
        _filterMenuValue = "";
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
    }

    // ── CheckBoxList filter support ─────────────────────────────────────

    internal void ToggleCheckBoxFilter(string field)
    {
        if (_checkBoxFilterField == field)
        {
            _checkBoxFilterField = null;
            return;
        }

        _checkBoxFilterField = field;

        // Extract distinct values from the source data
        _checkBoxFilterDistinct = GetDistinctValues(field);

        // Pre-select currently filtered values, or all if no filter active
        var existing = _state.FilterDescriptors
            .Where(f => f.Field == field && f.Operator == FilterOperator.Equals)
            .Select(f => f.Value?.ToString() ?? "")
            .ToList();

        _checkBoxFilterSelected = existing.Count > 0
            ? new HashSet<string>(existing)
            : new HashSet<string>(_checkBoxFilterDistinct);
    }

    internal void ToggleCheckBoxValue(string value)
    {
        if (_checkBoxFilterSelected.Contains(value))
            _checkBoxFilterSelected.Remove(value);
        else
            _checkBoxFilterSelected.Add(value);
    }

    internal async Task ApplyCheckBoxFilter()
    {
        if (_checkBoxFilterField == null) return;

        // Remove existing filters for this field
        _state.FilterDescriptors.RemoveAll(f => f.Field == _checkBoxFilterField);

        // If not all values are selected, add inclusion filters via composite
        if (_checkBoxFilterSelected.Count < _checkBoxFilterDistinct.Count && _checkBoxFilterSelected.Count > 0)
        {
            // Use composite OR filter: value == A OR value == B OR ...
            var composite = new CompositeFilterDescriptor
            {
                LogicalOperator = FilterCompositionOperator.Or,
                Filters = _checkBoxFilterSelected.Select(v => new FilterDescriptor
                {
                    Field = _checkBoxFilterField,
                    Operator = FilterOperator.Equals,
                    Value = v
                }).ToList()
            };

            // Remove existing composite for this field
            _state.CompositeFilterDescriptors.RemoveAll(c =>
                c.Filters.Count > 0 && c.Filters[0].Field == _checkBoxFilterField);
            _state.CompositeFilterDescriptors.Add(composite);
        }
        else
        {
            // All selected or none — remove composite filter for this field
            _state.CompositeFilterDescriptors.RemoveAll(c =>
                c.Filters.Count > 0 && c.Filters[0].Field == _checkBoxFilterField);
        }

        _checkBoxFilterField = null;
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
    }

    internal async Task ClearCheckBoxFilter()
    {
        if (_checkBoxFilterField == null) return;

        _state.FilterDescriptors.RemoveAll(f => f.Field == _checkBoxFilterField);
        _state.CompositeFilterDescriptors.RemoveAll(c =>
            c.Filters.Count > 0 && c.Filters[0].Field == _checkBoxFilterField);

        _checkBoxFilterField = null;
        _checkBoxFilterSelected.Clear();
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
    }

    private List<string> GetDistinctValues(string field)
    {
        if (Data is null) return new List<string>();

        var prop = typeof(TItem).GetProperty(field);
        if (prop == null) return new List<string>();

        return Data
            .Select(item => prop.GetValue(item)?.ToString() ?? "(null)")
            .Distinct()
            .OrderBy(v => v)
            .ToList();
    }

    /// <summary>Programmatically adds or replaces a filter on the specified field.</summary>
    public async Task AddFilter(FilterDescriptor filter)
    {
        var existing = _state.FilterDescriptors.FirstOrDefault(f => f.Field == filter.Field);
        if (existing != null)
            _state.FilterDescriptors.Remove(existing);
        _state.FilterDescriptors.Add(filter);
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
        StateHasChanged();
    }

    /// <summary>
    /// Removes all active filters (column filters, composite filters, and search box text) and reprocesses data.
    /// </summary>
    public async Task ClearFilters()
    {
        // Cancel any pending search debounce
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;

        _state.FilterDescriptors.Clear();
        _state.CompositeFilterDescriptors.Clear();
        _searchText = "";
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
        StateHasChanged();
    }

    /// <summary>Programmatically adds a composite filter group.</summary>
    public async Task AddCompositeFilter(CompositeFilterDescriptor composite)
    {
        _state.CompositeFilterDescriptors.Add(composite);
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
        StateHasChanged();
    }

    /// <summary>Removes all composite filter groups.</summary>
    public async Task ClearCompositeFilters()
    {
        _state.CompositeFilterDescriptors.Clear();
        _state.CurrentPage = 1;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Filter");
        StateHasChanged();
    }

    // ── Event Handlers: Selection ───────────────────────────────────────

    internal async Task HandleRowClick(TItem item, MouseEventArgs e)
    {
        if (SelectionMode != GridSelectionMode.None && !ShowCheckboxColumn)
        {
            await ToggleSelection(item);
        }

        if (OnRowClick.HasDelegate)
        {
            await OnRowClick.InvokeAsync(new GridRowClickEventArgs<TItem>
            {
                Item = item,
                EventArgs = e
            });
        }
    }

    internal async Task HandleRowDoubleClick(TItem item, MouseEventArgs e)
    {
        // In InCell mode, double-click enters edit mode
        if (EditMode == GridEditMode.InCell && !IsItemEditing(item))
        {
            await BeginEdit(item);
        }

        if (OnRowDoubleClick.HasDelegate)
        {
            await OnRowDoubleClick.InvokeAsync(new GridRowClickEventArgs<TItem>
            {
                Item = item,
                EventArgs = e
            });
        }
    }

    internal async Task HandleRowContextMenu(TItem item, MouseEventArgs e)
    {
        if (OnRowContextMenu.HasDelegate)
        {
            await OnRowContextMenu.InvokeAsync(new GridRowClickEventArgs<TItem>
            {
                Item = item,
                EventArgs = e
            });
        }
    }

    internal async Task ToggleSelection(TItem item)
    {
        if (SelectionMode == GridSelectionMode.Single)
        {
            _selectedItems.Clear();
            _selectedItems.Add(item);
        }
        else if (SelectionMode == GridSelectionMode.Multiple)
        {
            if (!_selectedItems.Remove(item))
                _selectedItems.Add(item);
        }

        await SelectedItemsChanged.InvokeAsync(_selectedItems.ToList());
        await NotifyStateChanged("Selection");
    }

    internal async Task OnCheckboxToggle(TItem item)
    {
        await ToggleSelection(item);
    }

    // ── Cell Selection ─────────────────────────────────────────────────

    internal async Task HandleCellClick(TItem item, string field, int rowIndex)
    {
        if (SelectionUnit != GridSelectionUnit.Cell) return;

        var key = (rowIndex, field);

        if (SelectionMode == GridSelectionMode.Single)
        {
            _selectedCellKeys.Clear();
            _selectedCellKeys.Add(key);
        }
        else if (SelectionMode == GridSelectionMode.Multiple)
        {
            if (_selectedCellKeys.Contains(key))
                _selectedCellKeys.Remove(key);
            else
                _selectedCellKeys.Add(key);
        }

        // Build cell references for the callback
        var cellRefs = _selectedCellKeys.Select(k =>
        {
            var displayItem = _displayedItems.ElementAtOrDefault(k.RowIndex);
            var prop = typeof(TItem).GetProperty(k.Field);
            return new GridCellReference<TItem>
            {
                Item = displayItem!,
                Field = k.Field,
                Value = prop?.GetValue(displayItem),
                RowIndex = k.RowIndex
            };
        }).Where(c => c.Item is not null).ToList();

        if (SelectedCellsChanged.HasDelegate)
            await SelectedCellsChanged.InvokeAsync(cellRefs);
    }

    internal bool IsCellSelected(int rowIndex, string field)
    {
        return SelectionUnit == GridSelectionUnit.Cell && _selectedCellKeys.Contains((rowIndex, field));
    }

    internal async Task OnSelectAllChanged(ChangeEventArgs e)
    {
        var selectAll = e.Value is true or "true";
        if (selectAll)
        {
            _selectedItems = new HashSet<TItem>(_displayedItems);
        }
        else
        {
            _selectedItems.Clear();
        }

        await SelectedItemsChanged.InvokeAsync(_selectedItems.ToList());
        await NotifyStateChanged("Selection");
    }

    // ── Event Handlers: Paging ──────────────────────────────────────────

    internal async Task GoToPreviousPage()
    {
        if (_state.CurrentPage > 1)
        {
            _state.CurrentPage--;
            await ProcessDataAsync();
            await NotifyPageChanged();
            await NotifyStateChanged("Page");
        }
    }

    internal async Task GoToNextPage()
    {
        if (_state.CurrentPage < TotalPages)
        {
            _state.CurrentPage++;
            await ProcessDataAsync();
            await NotifyPageChanged();
            await NotifyStateChanged("Page");
        }
    }

    internal async Task GoToPage(int page)
    {
        if (page < 1 || page > TotalPages || page == _state.CurrentPage) return;
        _state.CurrentPage = page;
        await ProcessDataAsync();
        await NotifyPageChanged();
        await NotifyStateChanged("Page");
    }

    internal async Task OnPaginationPageSizeChanged(int newSize)
    {
        if (newSize > 0 && newSize != _state.PageSize)
        {
            _state.PageSize = newSize;
            _state.CurrentPage = 1;
            await ProcessDataAsync();
            await PageSizeChanged.InvokeAsync(newSize);
            await NotifyPageChanged();
            await NotifyStateChanged("PageSize");
        }
    }

    internal async Task OnPageSizeDropdownChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newSize) && newSize > 0)
        {
            await OnPaginationPageSizeChanged(newSize);
        }
    }
}
