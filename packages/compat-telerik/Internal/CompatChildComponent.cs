using System;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Compat.Telerik.Internal;

/// <summary>
/// Base class for compat-shim child components that must resolve a cascading parent of a
/// known type (e.g. <c>TelerikGridColumn</c> inside <c>TelerikGrid</c>, or a future
/// <c>TelerikTabStripTab</c> inside <c>TelerikTabStrip</c>). Factors out:
/// <list type="bullet">
///   <item><description>The <c>[CascadingParameter]</c> lookup for the parent.</description></item>
///   <item><description>A hard guard that throws <see cref="InvalidOperationException"/> when the
///     component is rendered outside its expected parent — matches Telerik's own behavior where
///     <c>GridColumn</c>, <c>ValidationSummary</c>, etc. are meaningless outside their container.</description></item>
/// </list>
///
/// <para>This base lives in compat-telerik for now. If the pattern proves reusable when
/// compat-syncfusion / compat-devexpress / compat-infragistics start scaffolding, lift it to a
/// shared <c>compat-shared</c> package (see <c>icm/00_intake/output/compat-expansion-intake.md</c>
/// Decision 4 rationale).</para>
/// </summary>
/// <typeparam name="TParent">The expected cascading parent type.</typeparam>
public abstract class CompatChildComponent<TParent> : ComponentBase where TParent : class
{
    /// <summary>The cascading parent; <c>null</c> when rendered outside a parent of type <typeparamref name="TParent"/>.</summary>
    [CascadingParameter] protected TParent? Parent { get; set; }

    /// <summary>
    /// Name of the shim type (used for the "outside parent" exception message). Defaults to the
    /// runtime type name — override only if the shim needs a friendlier name in error output.
    /// </summary>
    protected virtual string ShimName => GetType().Name;

    /// <summary>
    /// Name of the expected parent type for the "outside parent" exception message. Defaults to
    /// the open <typeparamref name="TParent"/> name — override when the expected parent has a
    /// different Telerik-shaped marketing name than its CLR name.
    /// </summary>
    protected virtual string ParentName => typeof(TParent).Name;

    /// <summary>
    /// Asserts that <see cref="Parent"/> is non-null and returns it. Call at the top of
    /// <c>OnParametersSet</c> or <c>OnInitialized</c> in subclasses that require the parent to
    /// function. Mirrors Telerik's "column outside a grid throws" behavior.
    /// </summary>
    protected TParent RequireParent()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"compat-telerik: <{ShimName}> must be used inside a <{ParentName}>. " +
                $"See docs/compat-telerik-mapping.md.");
        }
        return Parent;
    }
}
