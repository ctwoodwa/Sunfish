using System.ComponentModel;
using Sunfish.Foundation.BusinessLogic.Authorization;
using Sunfish.Foundation.BusinessLogic.Enums;
using Sunfish.Foundation.BusinessLogic.Rules;

namespace Sunfish.Foundation.BusinessLogic;

/// <summary>
/// Base class for all Sunfish domain objects. Composes
/// <see cref="FieldManager"/>, <see cref="BusinessRuleEngine"/>,
/// <see cref="AuthorizationEngine"/>, and <see cref="UndoStack"/> into a
/// single, consistent property-access pipeline.
///
/// Subclasses declare one static <see cref="PropertyInfo{T}"/> per managed
/// property and read/write through <see cref="GetProperty{T}"/> and
/// <see cref="SetProperty{T}"/>.
/// </summary>
/// <typeparam name="T">The concrete subclass type (CRTP pattern).</typeparam>
public abstract class BusinessObjectBase<T> : INotifyPropertyChanged
    where T : BusinessObjectBase<T>
{
    // ── Infrastructure ──────────────────────────────────────────────────

    /// <summary>Per-instance value store and dirty tracker.</summary>
    protected internal FieldManager Fields { get; } = new();

    /// <summary>Validation rule registry.</summary>
    protected BusinessRuleEngine Rules { get; } = new();

    /// <summary>Authorization rule registry.</summary>
    protected AuthorizationEngine Authorization { get; } = new();

    /// <summary>Multi-level undo / redo stack.</summary>
    protected UndoStack Undo { get; } = new();

    // ── INotifyPropertyChanged ──────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // ── State queries ───────────────────────────────────────────────────

    /// <summary>True when any property has an uncommitted change.</summary>
    public bool IsDirty => Fields.IsDirty;

    /// <summary>True when all registered rules pass.</summary>
    public bool IsValid => Rules.IsValid(this);

    /// <summary>All currently broken rules.</summary>
    public IReadOnlyList<BrokenRule> BrokenRules => Rules.CheckRules(this);

    /// <summary>Broken rules for a specific property.</summary>
    public IReadOnlyList<BrokenRule> GetBrokenRulesFor<TValue>(PropertyInfo<TValue> property)
        => Rules.CheckRulesFor(this, property.Name);

    // ── Authorization helpers ───────────────────────────────────────────

    /// <summary>Returns true when the current principal can read this property.</summary>
    public bool CanReadProperty<TValue>(PropertyInfo<TValue> property, object? principal = null)
        => Authorization.CanRead(property.Name, principal);

    /// <summary>Returns true when the current principal can write this property.</summary>
    public bool CanWriteProperty<TValue>(PropertyInfo<TValue> property, object? principal = null)
        => Authorization.CanWrite(property.Name, principal);

    // ── Core accessors ──────────────────────────────────────────────────

    /// <summary>
    /// Reads a managed property value, enforcing the authorization rule.
    /// Throws <see cref="UnauthorizedAccessException"/> when read is denied.
    /// </summary>
    protected TValue GetProperty<TValue>(PropertyInfo<TValue> property, object? principal = null)
    {
        if (!Authorization.CanRead(property.Name, principal))
            throw new UnauthorizedAccessException(
                $"Read access denied for property '{property.Name}'.");

        return Fields.Read(property);
    }

    /// <summary>
    /// Writes a managed property value through the full pipeline:
    /// authorization → undo snapshot → field write → rule check → change notification.
    /// </summary>
    /// <returns><c>true</c> when the value changed.</returns>
    protected bool SetProperty<TValue>(PropertyInfo<TValue> property, TValue value, object? principal = null)
    {
        if (!Authorization.CanWrite(property.Name, principal))
            throw new UnauthorizedAccessException(
                $"Write access denied for property '{property.Name}'.");

        // Capture pre-change snapshot for undo before the write.
        Undo.Push(Fields.GetSnapshot());

        if (!Fields.Write(property, value))
        {
            // Value unchanged — discard the redundant snapshot.
            Undo.Undo(Fields.GetSnapshot()); // pop and throw away
            return false;
        }

        OnPropertyChanged(property.Name);
        return true;
    }

    // ── Undo / Redo ─────────────────────────────────────────────────────

    public bool CanUndo => Undo.CanUndo;
    public bool CanRedo => Undo.CanRedo;

    public void UndoEdit()
    {
        if (!Undo.CanUndo) return;
        var restored = Undo.Undo(Fields.GetSnapshot());
        Fields.RestoreSnapshot(restored);
        OnPropertyChanged(string.Empty); // notify all bindings
    }

    public void RedoEdit()
    {
        if (!Undo.CanRedo) return;
        var restored = Undo.Redo(Fields.GetSnapshot());
        Fields.RestoreSnapshot(restored);
        OnPropertyChanged(string.Empty);
    }

    // ── Save / discard ──────────────────────────────────────────────────

    /// <summary>Marks all properties clean and discards the undo history.</summary>
    public virtual void AcceptChanges()
    {
        Fields.MarkClean();
        Undo.Discard();
    }

    /// <summary>Rolls back to the oldest snapshot in the undo stack.</summary>
    public virtual void RejectChanges()
    {
        while (Undo.CanUndo) UndoEdit();
        Fields.MarkClean();
        Undo.Discard();
    }
}
