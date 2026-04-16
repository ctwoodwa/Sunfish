using Sunfish.Foundation.BusinessLogic.Enums;

namespace Sunfish.Foundation.BusinessLogic;

/// <summary>
/// A compile-time token that identifies a managed property on a
/// <see cref="BusinessObjectBase{T}"/>. Store one static instance per
/// property — it carries no per-instance state.
/// </summary>
/// <typeparam name="T">The CLR type of the property value.</typeparam>
public sealed class PropertyInfo<T>
{
    /// <summary>The property name used as the dictionary key in <see cref="FieldManager"/>.</summary>
    public string Name { get; }

    /// <summary>Value returned when no value has been explicitly set.</summary>
    public T DefaultValue { get; }

    /// <summary>
    /// The access mode applied before the authorization engine runs.
    /// Defaults to <see cref="AccessMode.ReadWrite"/>.
    /// </summary>
    public AccessMode InitialAccess { get; }

    public PropertyInfo(string name, T defaultValue = default!, AccessMode initialAccess = AccessMode.ReadWrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        DefaultValue = defaultValue;
        InitialAccess = initialAccess;
    }

    public override string ToString() => $"{typeof(T).Name} {Name} [{InitialAccess}]";
}
