using System.Text.Json;

namespace Sunfish.Foundation.Helpers;

/// <summary>
/// Reflection utilities for SunfishDataSheet property access and cloning.
/// </summary>
public static class GridReflectionHelper
{
    /// <summary>Gets the value of a named property from an item.</summary>
    public static object? GetValue<T>(T item, string field)
    {
        if (item is null || string.IsNullOrEmpty(field)) return null;
        return typeof(T).GetProperty(field)?.GetValue(item);
    }

    /// <summary>Sets the value of a named property on an item.</summary>
    public static void SetValue<T>(T item, string field, object? value)
    {
        if (item is null || string.IsNullOrEmpty(field)) return;
        var prop = typeof(T).GetProperty(field);
        if (prop is null || !prop.CanWrite) return;

        if (value is null)
        {
            if (Nullable.GetUnderlyingType(prop.PropertyType) != null || !prop.PropertyType.IsValueType)
                prop.SetValue(item, null);
            return;
        }

        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        try
        {
            if (value is JsonElement jsonElement)
            {
                value = DeserializeJsonElement(jsonElement, targetType);
            }
            else if (value.GetType() != targetType)
            {
                value = Convert.ChangeType(value, targetType);
            }
            prop.SetValue(item, value);
        }
        catch (Exception)
        {
            // Silently ignore type conversion failures
        }
    }

    /// <summary>Creates a deep clone of an item using JSON serialization.</summary>
    public static T DeepClone<T>(T source)
    {
        if (source is null) return default!;
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json)!;
    }

    private static object? DeserializeJsonElement(JsonElement element, Type targetType)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String when targetType == typeof(string) => element.GetString(),
            JsonValueKind.String when targetType == typeof(DateTime) => element.GetDateTime(),
            JsonValueKind.String when targetType == typeof(DateOnly) => DateOnly.FromDateTime(element.GetDateTime()),
            JsonValueKind.String when targetType == typeof(Guid) => element.GetGuid(),
            JsonValueKind.Number when targetType == typeof(int) => element.GetInt32(),
            JsonValueKind.Number when targetType == typeof(long) => element.GetInt64(),
            JsonValueKind.Number when targetType == typeof(decimal) => element.GetDecimal(),
            JsonValueKind.Number when targetType == typeof(double) => element.GetDouble(),
            JsonValueKind.Number when targetType == typeof(float) => element.GetSingle(),
            JsonValueKind.True or JsonValueKind.False when targetType == typeof(bool) => element.GetBoolean(),
            _ => JsonSerializer.Deserialize(element.GetRawText(), targetType)
        };
    }
}
