---
title: Business Object Design
page_title: AllocationScheduler Business Object Design
description: How to architect consumer-side business objects for the AllocationScheduler using CSLA-inspired patterns — managed properties, undo/redo serialization, business rules, authorization, change tracking, and event sourcing — while preserving Parse Don't Validate and value object discipline.
slug: allocation-scheduler-business-objects
tags: sunfish,blazor,allocation-scheduler,architecture,csla,business-objects,undo-redo,managed-properties,rules,authorization,security
published: True
position: 6
components: ["allocation-scheduler"]
---

# AllocationScheduler — Business Object Design

This guide describes how to model consumer-side business objects for the `AllocationScheduler`
write model. It draws from [CSLA.NET](https://github.com/MarimerLLC/csla) patterns — managed
properties, undo/redo serialization, `INotifyPropertyChanged`, business rules, and authorization —
and shows how they compose with the Parse Don't Validate and value object discipline described in
the Architecture Guide.

---

## Why Business Objects Benefit from CSLA-Inspired Design

A plain POCO with `{ get; set; }` properties cannot:

- Run validation rules when a property changes
- Check authorization at the individual field level before returning or storing a value
- Track which fields have changed since last save
- Snapshot and restore state for undo/redo without re-querying the server
- Propagate `INotifyPropertyChanged` reliably across related fields

The `AllocationScheduler` domain has all five requirements. Centralizing them in a
`BusinessObjectBase<T>` costs far less than duplicating the same logic across services,
command handlers, and Razor components.

---

## The Managed Property Pattern

Every managed property is declared as a static `PropertyInfo<T>` token — registered once per
type, shared by all instances. The token carries the property name, type, default value, and access
flags. Every accessor method accepts a token, not a magic string.

```csharp
// src/Sunfish.Core/Base/BusinessObjectBase.cs
namespace Sunfish.Core.Base;

public abstract class BusinessObjectBase<T> : INotifyPropertyChanged, ISupportUndo
    where T : BusinessObjectBase<T>
{
    // ── Static property registration ─────────────────────────────────

    private static readonly Dictionary<string, IPropertyInfo> _registeredProperties = new();

    protected static PropertyInfo<TValue> RegisterProperty<TValue>(
        string name,
        TValue defaultValue = default!,
        PropertyAccess access = PropertyAccess.ReadWrite)
    {
        var info = new PropertyInfo<TValue>(name, defaultValue, access);
        _registeredProperties[name] = info;
        return info;
    }

    // ── Instance state ────────────────────────────────────────────────
    protected readonly FieldManager _fieldManager = new();
    protected readonly BusinessRuleEngine _rules;
    private readonly AuthorizationEngine _auth;
    private IPrincipal _principal = Thread.CurrentPrincipal!;
    private bool _isDirty;
    private readonly Stack<byte[]> _undoStack = new();

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

---

## Authorization: The Asymmetric Read/Write Contract

### `GetProperty` — Silent Default on Denied Read

`GetProperty` checks authorization before returning a value. **If the current user is not
authorized to read the property, the property's registered `defaultValue` is returned silently.
No exception is thrown.**

```csharp
protected TValue GetProperty<TValue>(PropertyInfo<TValue> prop)
{
    // Authorization check — silent default if denied
    if (!CanReadProperty(prop))
        return prop.DefaultValue;   // real value never leaves the object

    return _fieldManager.GetValue(prop);
}
```

This is intentional. In Blazor and other data-binding frameworks, the binding engine reads
property getters **declaratively before the UI developer can intercept the call**. A getter that
throws on a denied read would crash data binding or surface confusing framework exceptions before
any application code could handle them.

The contract is: **the property silently lies by returning its default value**. A well-built UI
will never display even that default, because it will have already called `CanReadProperty` to
hide or disable the field before rendering.

### `SetProperty` — `SecurityException` on Denied Write

`SetProperty` checks authorization first. **If the current user is not authorized to write the
property, a `SecurityException` is thrown immediately.** No value is stored, no rules run, no
events fire.

```csharp
protected void SetProperty<TValue>(PropertyInfo<TValue> prop, TValue value)
{
    // Authorization check — throws if denied
    if (!CanWriteProperty(prop))
        throw new SecurityException(
            $"Write access to '{prop.Name}' denied for the current user. " +
            $"Ensure the UI has called CanWriteProperty before allowing an edit.");

    // No-op guard — skip pipeline entirely if value is unchanged
    var current = _fieldManager.GetValue(prop);
    if (EqualityComparer<TValue>.Default.Equals(current, value))
        return;

    _fieldManager.SetValue(prop, value);
    MarkDirty();
    _rules.Run(prop, this);
    RaisePropertyChanged(prop.Name);
}
```

**Why throw here but not on read?** Writing is an intentional, user-initiated act. A well-built
UI will have called `CanWriteProperty` and disabled or hidden the editing control before the user
could attempt the edit. If `SetProperty` is called anyway on a denied field, the only explanation
is that a UI developer introduced a bug by allowing the edit path to proceed without checking
authorization. Throwing `SecurityException` surfaces that bug loudly rather than silently
discarding the change.

> The exception message deliberately names the property and states the remedy, so developers see
> a useful message in the debugger rather than a generic access-denied stack trace.

### `CanReadProperty` and `CanWriteProperty`

Both are public and safe to call from Razor markup or code-behind. Neither throws.

```csharp
// src/Sunfish.Core/Base/BusinessObjectBase.cs

/// <summary>
/// Returns true if the current principal may read this property.
/// Call from UI code to decide whether to render or hide a field.
/// Never throws.
/// </summary>
public bool CanReadProperty<TValue>(PropertyInfo<TValue> prop) =>
    _auth.Evaluate(prop, _principal, this)
        is not PropertyAccess.Denied
        and not PropertyAccess.WriteOnly;

/// <summary>
/// Returns true if the current principal may write this property.
/// Call from UI code to decide whether to enable or disable an editing control.
/// Never throws. SetProperty WILL throw SecurityException if this returns false.
/// </summary>
public bool CanWriteProperty<TValue>(PropertyInfo<TValue> prop) =>
    _auth.Evaluate(prop, _principal, this)
        is not PropertyAccess.Denied
        and not PropertyAccess.ReadOnly;

// String overloads for Razor markup where the static token is not in scope
public bool CanReadProperty(string propertyName)  =>
    _registeredProperties.TryGetValue(propertyName, out var p) && CanReadProperty(p);

public bool CanWriteProperty(string propertyName) =>
    _registeredProperties.TryGetValue(propertyName, out var p) && CanWriteProperty(p);
```

---

## Resource-Level Permission Matrix

The `AllocationScheduler` exposes three distinct value dimensions on every row:

| Dimension | Property | Description |
|---|---|---|
| **Hours** | `AllocationValue` (when `Unit = Hours`) | Labor hours allocated per time bucket |
| **Cost** | `AllocationValue` (when `Unit = Currency`) | Dollar or budget value per time bucket |
| **Both** | `AllocationValue` (dual-mode) | Hours and derived cost visible simultaneously |

Different user roles see different columns. This is an authorization concern, not a display
concern — the correct place to enforce it is inside the business object's auth rules, so that
`GetProperty` returns only what the principal is permitted to see regardless of how the component
is configured.

### Permission Scenarios

```
Role                  | Hours | Cost  | Both
─────────────────────────────────────────────
Resource (self)       |  ✅   |  ❌   |  ❌   sees only their own hours
Resource Manager      |  ✅   |  ❌   |  ❌   sees hours for their team
Project Manager       |  ✅   |  ✅   |  ✅   sees hours and costs
Finance               |  ❌   |  ✅   |  ❌   sees cost totals only, not hours
Executive             |  ❌   |  ✅   |  ❌   summary costs, no individual hours
Portfolio Analyst     |  ✅   |  ✅   |  ✅   full access for planning
```

These are defaults. Actual permissions compose with:

- **WBS node scope** — a PM may only see hours for deliverables they own
- **Scenario lock** — promoted baselines are read-only for all roles
- **Resource privacy** — an HR-flagged resource's rate is visible only to Finance and HR

### Authorization Rules for Hour/Cost Visibility

```csharp
// src/Sunfish.Core/Contracts/IAuthorizationRule.cs
namespace Sunfish.Core.Contracts;

public interface IAuthorizationRule
{
    /// <summary>
    /// Evaluate access for one property on one object instance.
    /// Must be synchronous and allocation-free — called on every property access.
    /// </summary>
    PropertyAccess Evaluate(
        IPropertyInfo prop,
        IPrincipal principal,
        IBusinessObject target);
}
```

```csharp
// Authorization rule: controls which value dimension a principal can see
public sealed class AllocationValueVisibilityRule : IAuthorizationRule
{
    public PropertyAccess Evaluate(
        IPropertyInfo prop,
        IPrincipal principal,
        IBusinessObject target)
    {
        // Only applies to the AllocationValue property
        if (prop.Name != nameof(AllocationEntry.Value))
            return PropertyAccess.ReadWrite;

        if (target is not AllocationEntry entry)
            return PropertyAccess.ReadWrite;

        bool canSeeHours = principal.IsInRole("ResourceManager")
                        || principal.IsInRole("ProjectManager")
                        || principal.IsInRole("PortfolioAnalyst")
                        || IsOwnResource(principal, entry);

        bool canSeeCost  = principal.IsInRole("Finance")
                        || principal.IsInRole("ProjectManager")
                        || principal.IsInRole("PortfolioAnalyst")
                        || principal.IsInRole("Executive");

        // Current value dimension must match what the principal can see
        bool valueIsHours    = entry.Value.Unit == AllocationUnit.Hours;
        bool valueIsCurrency = entry.Value.Unit == AllocationUnit.Currency;

        if (valueIsHours    && !canSeeHours)  return PropertyAccess.Denied;
        if (valueIsCurrency && !canSeeCost)   return PropertyAccess.Denied;

        // Write is also gated — Finance sees cost totals but cannot edit allocations
        bool canWrite = principal.IsInRole("ProjectManager")
                     || principal.IsInRole("PortfolioAnalyst")
                     || principal.IsInRole("ResourceManager");

        return canWrite ? PropertyAccess.ReadWrite : PropertyAccess.ReadOnly;
    }

    private static bool IsOwnResource(IPrincipal p, AllocationEntry e) =>
        p.Identity?.Name is { } name &&
        string.Equals(name, e.ResourceIdentifier, StringComparison.OrdinalIgnoreCase);
}
```

```csharp
// Authorization rule: WBS node scope — PM sees only deliverables they own
public sealed class WbsOwnershipRule : IAuthorizationRule
{
    public PropertyAccess Evaluate(
        IPropertyInfo prop,
        IPrincipal principal,
        IBusinessObject target)
    {
        if (target is not AllocationEntry entry) return PropertyAccess.ReadWrite;

        // ProjectManagers who are not the owner of this WBS node get read-only
        if (principal.IsInRole("ProjectManager")
            && entry.WbsOwnerIdentifier is { } owner
            && !string.Equals(principal.Identity?.Name, owner,
                              StringComparison.OrdinalIgnoreCase))
        {
            return PropertyAccess.ReadOnly;
        }

        return PropertyAccess.ReadWrite;
    }
}
```

```csharp
// Authorization rule: locked baseline sets are always read-only
public sealed class LockedBaselineRule : IAuthorizationRule
{
    public PropertyAccess Evaluate(
        IPropertyInfo prop,
        IPrincipal principal,
        IBusinessObject target)
    {
        if (target is AllocationEntry { IsBaselineLocked: true })
            return PropertyAccess.ReadOnly;   // all users, all properties

        return PropertyAccess.ReadWrite;
    }
}
```

### The Authorization Engine Composes Rules with Most-Restrictive Wins

```csharp
// src/Sunfish.Core/Base/AuthorizationEngine.cs
public sealed class AuthorizationEngine
{
    private readonly IReadOnlyList<IAuthorizationRule> _rules;

    public AuthorizationEngine(IEnumerable<IAuthorizationRule> rules) =>
        _rules = rules.ToList();

    public PropertyAccess Evaluate(
        IPropertyInfo prop,
        IPrincipal principal,
        IBusinessObject target)
    {
        // Evaluate all rules; Denied beats ReadOnly beats ReadWrite
        var verdict = PropertyAccess.ReadWrite;

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(prop, principal, target);

            // Denied is terminal — short-circuit
            if (result == PropertyAccess.Denied) return PropertyAccess.Denied;

            // ReadOnly overrides ReadWrite; WriteOnly overrides ReadWrite
            if (result != PropertyAccess.ReadWrite)
                verdict = result;
        }

        return verdict;
    }
}
```

---

## Blazor Integration: `CanReadProperty` Drives Rendering

The UI calls `CanReadProperty` and `CanWriteProperty` before rendering every sensitive field.
The component renders neither the value nor a placeholder — it renders **nothing** for denied
fields, consistent with the silent-default contract.

```razor
@* Hours column — visible to ResourceManager, ProjectManager, PortfolioAnalyst, own resource *@
@if (Entry.CanReadProperty(nameof(AllocationEntry.Value))
     && Entry.Value.Unit == AllocationUnit.Hours)
{
    @if (Entry.CanWriteProperty(nameof(AllocationEntry.Value)))
    {
        <input type="number"
               value="@Entry.Value.Amount"
               @onchange="OnValueChanged"
               class="cell-input" />
    }
    else
    {
        <span class="cell-readonly">@Entry.Value.Amount</span>
    }
}

@* Cost column — visible to Finance, ProjectManager, PortfolioAnalyst, Executive *@
@if (Entry.CanReadProperty(nameof(AllocationEntry.BudgetValue))
     && Entry.Value.Unit == AllocationUnit.Currency)
{
    <span class="cell-cost @(Entry.CanWriteProperty(nameof(AllocationEntry.BudgetValue)) ? "" : "readonly")">
        @Entry.BudgetValue.ToString("C")
    </span>
}
```

Columns that the current user cannot see are omitted entirely from the component's column
definition, so the layout does not contain empty ghost columns. The `AllocationScheduler`
component's column configuration is driven by an `AuthorizedColumnSet` built from
`CanReadProperty` calls before the grid renders.

```csharp
// Build the authorized column set once per render cycle
private AuthorizedColumnSet BuildColumnSet(AllocationEntry entry) => new()
{
    ShowHours  = entry.CanReadProperty(nameof(AllocationEntry.Value))
                 && entry.Value.Unit == AllocationUnit.Hours,
    ShowCost   = entry.CanReadProperty(nameof(AllocationEntry.BudgetValue)),
    CanEditAny = entry.CanWriteProperty(nameof(AllocationEntry.Value))
                 || entry.CanWriteProperty(nameof(AllocationEntry.BudgetValue))
};
```

---

## The Eight Accessor Methods

### Full Reference

| Method | Auth | Runs rules | Marks dirty | Raises events | When to use |
|---|---|---|---|---|---|
| `GetProperty(prop)` | ✅ Read — **silent default if denied** | — | — | — | Public property getter bound to UI |
| `ReadProperty(prop)` | ❌ | — | — | — | Internal reads inside rule methods, serialization |
| `SetProperty(prop, v)` | ✅ Write — **`SecurityException` if denied** | ✅ | ✅ | ✅ | Public property setter driven by user intent |
| `LoadProperty(prop, v)` | ❌ | ❌ | ❌ | ❌ | Data reconstitution from events or DB |
| `GetPropertyConvert(prop, fn)` | ✅ Read — **silent default if denied** | — | — | — | Backing type ≠ property type, with auth |
| `ReadPropertyConvert(prop, fn)` | ❌ | — | — | — | Backing type ≠ property type, internal |
| `SetPropertyConvert(prop, v, fn)` | ✅ Write — **`SecurityException` if denied** | ✅ | ✅ | ✅ | Backing type ≠ property type, user intent |
| `LoadPropertyConvert(prop, v, fn)` | ❌ | ❌ | ❌ | ❌ | Backing type ≠ property type, reconstitution |

### Critical: Why Rule Methods Must Use `ReadProperty`, Not `GetProperty`

Business rule implementations that read sibling properties to evaluate a cross-field rule **must
use `ReadProperty`**, never `GetProperty`. If a rule evaluating `Value` calls
`GetProperty(BillableRateProp)` and the current user cannot see `BillableRate`, `GetProperty`
returns `0m` (the default) silently — and the rule evaluates against a false value. The rule
engine sees no indication anything is wrong; it just evaluates incorrect data.

```csharp
// WRONG — if the current user can't read BillableRate, rate will be 0m
public class CapacityBudgetRule : BusinessRule
{
    public override RuleResult Execute(IBusinessObject target)
    {
        var entry = (AllocationEntry)target;
        var rate  = entry.GetProperty(AllocationEntry.BillableRateProp); // ← dangerous
        var cost  = entry.Value.Amount * rate;
        // ...
    }
}

// CORRECT — ReadProperty always returns the real value regardless of principal
public class CapacityBudgetRule : BusinessRule
{
    public override RuleResult Execute(IBusinessObject target)
    {
        var entry = (AllocationEntry)target;
        var rate  = entry.ReadProperty(AllocationEntry.BillableRateProp); // ← safe
        var cost  = entry.Value.Amount * rate;
        // ...
    }
}
```

Rule execution is an internal, system-initiated operation. The principal's visibility settings
do not — and must not — influence rule evaluation. Rules must always see the full, real state of
the object.

---

## `SetProperty` Pipeline (Full Detail)

```
SetProperty(prop, value)
  │
  ├─ 1. CanWriteProperty(prop)?
  │      └─ false → throw SecurityException
  │               (UI bug: caller should have checked CanWriteProperty first)
  │
  ├─ 2. ReadProperty(prop) == value?          ← uses ReadProperty, not GetProperty
  │      └─ true → return early
  │               (no dirty noise, no rule re-run for no-op edits)
  │
  ├─ 3. FieldManager.SetValue(prop, value)
  │
  ├─ 4. MarkDirty()
  │      └─ _isDirty = true
  │         RaisePropertyChanged(nameof(IsDirty))
  │         RaisePropertyChanged(nameof(IsSavable))
  │
  ├─ 5. BusinessRuleEngine.Run(prop, this)
  │      ├─ evaluates rules registered directly to prop
  │      ├─ evaluates cross-field rules that list prop in AffectedProperties
  │      │   all cross-field rule methods must use ReadProperty internally
  │      └─ for each affected property p:
  │           RaisePropertyChanged(nameof(IsValid))
  │           RaisePropertyChanged(p.Name)
  │
  └─ 6. RaisePropertyChanged(prop.Name)
```

Step 2 uses `ReadProperty` (not `GetProperty`) to get the current value for equality comparison,
so the no-op guard is never fooled by a silently-returned default value.

---

## Undo/Redo: Serialization Strategy

Business objects must be serializable for undo/redo. The undo mechanism snapshots only the
**FieldManager's value dictionary** — rules, events, and authorization metadata are stateless
and reconstructed.

```csharp
public void BeginEdit()  => _undoStack.Push(SerializeFields());

public void CancelEdit()
{
    if (_undoStack.TryPop(out var snapshot))
    {
        DeserializeFields(snapshot);  // uses LoadProperty internally — no rules, no events
        _rules.RunAll(this);          // re-evaluate all rules against restored values
        RaiseAllPropertiesChanged();  // force Blazor to re-render all bound fields
    }
}

public void ApplyEdit() => _undoStack.TryPop(out _);
public bool CanUndo     => _undoStack.Count > 0;
```

`DeserializeFields` uses `LoadProperty` for every field — no auth checks, no rules, no dirty
marking — so the snapshot restores cleanly regardless of the current principal's permissions.

Use **MessagePack** (not `BinaryFormatter`, which is removed in .NET 8; not `System.Text.Json`,
which is verbose for binary snapshots). Register custom formatters for `AllocationValue` and
`TimeBucket`.

---

## Business Rules

Business rules are registered to property tokens and run automatically from `SetProperty`.
They always read sibling properties via `ReadProperty`.

```csharp
protected override void AddBusinessRules(BusinessRuleEngine rules)
{
    // Value must be non-negative
    rules.AddRule(ValueProp,
        new LambdaRule<AllocationValue>("ValueNonNegative",
            v => v.Amount >= 0,
            "Allocation value cannot be negative."));

    // Cross-field: runs when Value OR Bucket changes
    rules.AddRule(
        affectedProperties: new[] { ValueProp, BucketProp },
        new CapacityCheckRule());   // uses ReadProperty for all sibling reads

    // Conditional required
    rules.AddRule(TaskNameProp,
        new RequiredWhenRule(
            condition: self => ((AllocationEntry)self).ReadProperty(ValueProp).Amount > 0,
            message: "Task name is required when hours are assigned."));
}
```

---

## Change Tracking

```csharp
public bool IsDirty     => _fieldManager.HasChanges;
public bool IsNew       { get; private set; }
public bool IsDeleted   { get; private set; }
public bool IsSavable   => IsValid && IsDirty && !IsBusy;
public IReadOnlySet<string> ChangedProperties => _fieldManager.ChangedPropertyNames;

public void MarkClean() => _fieldManager.AcceptChanges();
```

After a successful command dispatch, the application layer calls `MarkClean()`. `ChangedProperties`
is read before dispatch to build a minimal command containing only modified fields.

---

## Component Integration Flow

```
User edits cell
     │
OnCellEdited(CellEditedArgs<AllocationReadModel>)
     │
     ├─ Parse typed IDs at boundary (ResourceId.Parse, TaskId.Parse, etc.)
     ├─ entry.BeginEdit()                     ← snapshot pushed onto undo stack
     ├─ entry.Value = AllocationValue.Hours(newAmount)
     │    └─ SetProperty:
     │         CanWriteProperty?
     │           locked baseline? → SecurityException (UI bug)
     │           wrong role?      → SecurityException (UI bug)
     │         No-op if value unchanged (uses ReadProperty for comparison)
     │         MarkDirty
     │         RunBusinessRules (ValueNonNegative, CapacityCheck, ...)
     │         RaisePropertyChanged(nameof(Value))
     │
     ├─ entry.IsValid?
     │    false → entry.CancelEdit()           ← restore snapshot, run rules
     │             surface entry.BrokenRules in component inline
     │             return
     │
     ├─ await Commands.DispatchAsync(new EditAllocationCommand(
     │       AllocationId: entry.AllocationId,
     │       ChangedFields: entry.ChangedProperties,
     │       NewValue:      entry.ReadProperty(ValueProp)))
     │
     ├─ entry.ApplyEdit()                      ← discard snapshot
     ├─ entry.MarkClean()
     └─ _plan = BuildReadModels()              ← project BOs → flat DTOs for component

User presses Ctrl+Z (OnUndoRequested)
     │
     ├─ entry.CanUndo? → entry.CancelEdit()   ← restore snapshot, run rules, raise events
     └─ _plan = BuildReadModels()             ← no server round-trip needed
```

---

## Namespace Placement in `Sunfish.Core`

Following the existing structure of [`src/Sunfish.Core`](https://github.com/ctwoodwa/Sunfish/tree/workInProgress/src/Sunfish.Core):

```
src/Sunfish.Core/
├── Base/
│   ├── BusinessObjectBase.cs           ← abstract base with FieldManager + undo stack
│   ├── FieldManager.cs                 ← property value dictionary + change tracking
│   ├── BusinessRuleEngine.cs           ← rule registration + execution
│   └── AuthorizationEngine.cs          ← IAuthorizationRule composition (most-restrictive wins)
├── Contracts/
│   ├── IBusinessObject.cs
│   ├── IPropertyInfo.cs
│   ├── IAuthorizationRule.cs
│   ├── ISupportUndo.cs
│   └── IManageProperties.cs
├── Enums/
│   ├── PropertyAccess.cs               ← ReadWrite | ReadOnly | WriteOnly | Denied
│   ├── RuleSeverity.cs                 ← Error | Warning | Information
│   └── AllocationUnit.cs               ← Hours | Currency
└── Models/
    └── AllocationEntry.cs              ← concrete business object
```

---

## See Also

* [AllocationScheduler Architecture Guide](slug:allocation-scheduler-architecture)
* [AllocationScheduler Overview](slug:allocation-scheduler-overview)
* [Scenario Planning](slug:allocation-scheduler-scenario-planning)
* [Editing Grain Design Decision](slug:allocation-scheduler-editing-grain)
* [CSLA.NET — ISupportUndo](https://github.com/MarimerLLC/csla/blob/main/Source/Csla/Core/ISupportUndo.cs)
* [CSLA.NET — UndoableBase](https://github.com/MarimerLLC/csla/blob/main/Source/Csla/Core/UndoableBase.cs)
* [CSLA.NET — ManagedObjectBase](https://github.com/MarimerLLC/csla/blob/main/Source/Csla/Core/ManagedObjectBase.cs)
* [CSLA.NET — IManageProperties](https://github.com/MarimerLLC/csla/blob/main/Source/Csla/Core/IManageProperties.cs)
