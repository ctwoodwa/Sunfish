namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the type of data change that occurred.
/// </summary>
public enum ChangeType
{
    /// <summary>A new record was created.</summary>
    Created,

    /// <summary>An existing record was updated.</summary>
    Updated,

    /// <summary>A record was deleted.</summary>
    Deleted,

    /// <summary>A boolean state was toggled.</summary>
    Toggled
}

/// <summary>
/// Specifies the comparison operator for a data filter expression.
/// </summary>
public enum FilterOperator
{
    /// <summary>Value equals the filter value.</summary>
    Equals,

    /// <summary>Value does not equal the filter value.</summary>
    NotEquals,

    /// <summary>Value contains the filter substring.</summary>
    Contains,

    /// <summary>Value starts with the filter substring.</summary>
    StartsWith,

    /// <summary>Value ends with the filter substring.</summary>
    EndsWith,

    /// <summary>Value is greater than the filter value.</summary>
    GreaterThan,

    /// <summary>Value is greater than or equal to the filter value.</summary>
    GreaterThanOrEqual,

    /// <summary>Value is less than the filter value.</summary>
    LessThan,

    /// <summary>Value is less than or equal to the filter value.</summary>
    LessThanOrEqual,

    /// <summary>Value is null.</summary>
    IsNull,

    /// <summary>Value is not null.</summary>
    IsNotNull
}

/// <summary>
/// Specifies the direction of a sort operation.
/// </summary>
public enum SortDirection
{
    /// <summary>Sort from lowest to highest (A-Z, 0-9).</summary>
    Ascending,

    /// <summary>Sort from highest to lowest (Z-A, 9-0).</summary>
    Descending
}

/// <summary>
/// Specifies the logical operator for combining multiple filter conditions.
/// </summary>
public enum FilterCompositionOperator
{
    /// <summary>All filter conditions must match.</summary>
    And,

    /// <summary>Any filter condition must match.</summary>
    Or
}
