using Microsoft.AspNetCore.Components;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Query-builder UI. Users compose a tree of filter rows (field + operator + value)
/// inside AND/OR groups. The root <see cref="Value"/> is a
/// <see cref="FilterCompositeDescriptor"/> that maps cleanly to data-layer filter
/// pipelines (see <c>SunfishDataGrid</c>).
/// </summary>
/// <typeparam name="TItem">
/// The shape of the items being filtered. Not currently used by the MVP layout but
/// reserved for future metadata-driven field discovery.
/// </typeparam>
public partial class SunfishFilter<TItem> : SunfishComponentBase
{
    /// <summary>
    /// Fields offered in the field dropdown. Each entry controls the operator list
    /// and the value-input type for its rows.
    /// </summary>
    [Parameter] public IList<FilterField> Fields { get; set; } = new List<FilterField>();

    /// <summary>
    /// The filter tree root. When null, the component seeds an empty AND group.
    /// </summary>
    [Parameter] public FilterDescriptor? Value { get; set; }

    /// <summary>Raised whenever the internal tree mutates (field, op, value, add, remove).</summary>
    [Parameter] public EventCallback<FilterDescriptor> ValueChanged { get; set; }

    /// <summary>
    /// Raised when the user presses Apply. Receives the same descriptor that was last
    /// emitted via <see cref="ValueChanged"/>.
    /// </summary>
    [Parameter] public EventCallback<FilterDescriptor> OnApply { get; set; }

    /// <summary>The effective root — always a composite so users can add new rows/groups.</summary>
    protected FilterCompositeDescriptor _root = new();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Value is FilterCompositeDescriptor composite)
        {
            _root = composite;
        }
        else if (Value is FilterLeafDescriptor leaf)
        {
            _root = new FilterCompositeDescriptor
            {
                Logic = FilterLogic.And,
                Filters = new List<FilterDescriptor> { leaf }
            };
        }
        else if (Value is null)
        {
            // Keep existing root on repeat sets so user edits persist when parent re-renders
            // without mutating the bound value.
        }
    }

    /// <summary>Locate the declared field metadata for a given leaf field name.</summary>
    protected FilterField? ResolveField(string fieldName)
        => Fields.FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.Ordinal));

    /// <summary>
    /// Compute the operator list for a field, falling back to type-appropriate defaults
    /// when <see cref="FilterField.Operators"/> is empty.
    /// </summary>
    protected IEnumerable<FilterOperator> OperatorsFor(FilterField? field)
    {
        if (field is not null && field.Operators.Count > 0)
        {
            return field.Operators;
        }

        var type = field?.Type ?? FilterFieldType.Text;
        return type switch
        {
            FilterFieldType.Number => new[]
            {
                FilterOperator.Equals, FilterOperator.NotEquals,
                FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual,
                FilterOperator.LessThan, FilterOperator.LessThanOrEqual,
                FilterOperator.IsNull, FilterOperator.IsNotNull
            },
            FilterFieldType.Date => new[]
            {
                FilterOperator.Equals, FilterOperator.NotEquals,
                FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual,
                FilterOperator.LessThan, FilterOperator.LessThanOrEqual,
                FilterOperator.IsNull, FilterOperator.IsNotNull
            },
            FilterFieldType.Boolean => new[]
            {
                FilterOperator.Equals, FilterOperator.NotEquals
            },
            FilterFieldType.Enum => new[]
            {
                FilterOperator.Equals, FilterOperator.NotEquals,
                FilterOperator.IsNull, FilterOperator.IsNotNull
            },
            _ => new[]
            {
                FilterOperator.Contains, FilterOperator.StartsWith, FilterOperator.EndsWith,
                FilterOperator.Equals, FilterOperator.NotEquals,
                FilterOperator.IsNull, FilterOperator.IsNotNull
            }
        };
    }

    /// <summary>Human-readable label for a given operator.</summary>
    protected static string FormatOperator(FilterOperator op) => op switch
    {
        FilterOperator.Equals => "Is Equal To",
        FilterOperator.NotEquals => "Is Not Equal To",
        FilterOperator.Contains => "Contains",
        FilterOperator.StartsWith => "Starts With",
        FilterOperator.EndsWith => "Ends With",
        FilterOperator.GreaterThan => "Greater Than",
        FilterOperator.LessThan => "Less Than",
        FilterOperator.GreaterThanOrEqual => "Greater or Equal",
        FilterOperator.LessThanOrEqual => "Less or Equal",
        FilterOperator.IsNull => "Is Null",
        FilterOperator.IsNotNull => "Is Not Null",
        _ => op.ToString()
    };

    // ── Tree mutations ──────────────────────────────────────────────────────

    /// <summary>Append a new leaf expression to a composite.</summary>
    protected async Task AddLeaf(FilterCompositeDescriptor parent)
    {
        var defaultField = Fields.FirstOrDefault();
        parent.Filters.Add(new FilterLeafDescriptor
        {
            Field = defaultField?.Name ?? string.Empty,
            Operator = OperatorsFor(defaultField).FirstOrDefault()
        });
        await NotifyChanged();
    }

    /// <summary>Append a nested AND/OR group to a composite.</summary>
    protected async Task AddGroup(FilterCompositeDescriptor parent)
    {
        parent.Filters.Add(new FilterCompositeDescriptor { Logic = FilterLogic.And });
        await NotifyChanged();
    }

    /// <summary>Remove a child (leaf or composite) from its parent.</summary>
    protected async Task Remove(FilterCompositeDescriptor parent, FilterDescriptor child)
    {
        parent.Filters.Remove(child);
        await NotifyChanged();
    }

    /// <summary>Update the logic operator on a composite group.</summary>
    protected async Task OnLogicChanged(FilterCompositeDescriptor composite, ChangeEventArgs e)
    {
        if (Enum.TryParse<FilterLogic>(e.Value?.ToString(), out var logic))
        {
            composite.Logic = logic;
            await NotifyChanged();
        }
    }

    /// <summary>Update the field path on a leaf, reselecting a default operator for the new type.</summary>
    protected async Task OnFieldChanged(FilterLeafDescriptor leaf, ChangeEventArgs e)
    {
        leaf.Field = e.Value?.ToString() ?? string.Empty;
        var field = ResolveField(leaf.Field);
        var ops = OperatorsFor(field).ToList();
        if (!ops.Contains(leaf.Operator))
        {
            leaf.Operator = ops.FirstOrDefault();
        }
        leaf.Value = null;
        await NotifyChanged();
    }

    /// <summary>Update the operator on a leaf.</summary>
    protected async Task OnOperatorChanged(FilterLeafDescriptor leaf, ChangeEventArgs e)
    {
        if (Enum.TryParse<FilterOperator>(e.Value?.ToString(), out var op))
        {
            leaf.Operator = op;
            if (op is FilterOperator.IsNull or FilterOperator.IsNotNull)
            {
                leaf.Value = null;
            }
            await NotifyChanged();
        }
    }

    /// <summary>Update the value on a leaf.</summary>
    protected async Task OnValueChanged(FilterLeafDescriptor leaf, ChangeEventArgs e)
    {
        leaf.Value = e.Value;
        await NotifyChanged();
    }

    /// <summary>Fire <see cref="ValueChanged"/> with the current root descriptor.</summary>
    protected async Task NotifyChanged()
    {
        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(_root);
        }
    }

    /// <summary>Fire <see cref="OnApply"/> with the current root descriptor.</summary>
    protected async Task ApplyFilter()
    {
        await NotifyChanged();
        if (OnApply.HasDelegate)
        {
            await OnApply.InvokeAsync(_root);
        }
    }
}
