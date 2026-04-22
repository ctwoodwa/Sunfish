using System;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Compat.Shared;

/// <summary>
/// Base class for compat-shim child components that must resolve a cascading parent of a
/// known type (e.g. a grid-column shim inside its grid, or a tab-strip-tab shim inside its
/// tab strip). Factors out:
/// <list type="bullet">
///   <item><description>The <c>[CascadingParameter]</c> lookup for the parent.</description></item>
///   <item><description>A hard guard that throws <see cref="InvalidOperationException"/> when the
///     component is rendered outside its expected parent — matching most commercial vendors'
///     behavior where column / validation / tab children are meaningless outside their container.</description></item>
/// </list>
///
/// <para>Used by <c>compat-telerik</c>, <c>compat-syncfusion</c>, <c>compat-devexpress</c>,
/// and <c>compat-infragistics</c>. Vendor packages typically subclass indirectly (via a
/// vendor-specific intermediate) to layer vendor-branded error messages or extra cascading
/// parameters on top of this base.</para>
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
    /// different vendor-shaped marketing name than its CLR name.
    /// </summary>
    protected virtual string ParentName => typeof(TParent).Name;

    /// <summary>
    /// Asserts that <see cref="Parent"/> is non-null and returns it. Call at the top of
    /// <c>OnParametersSet</c> or <c>OnInitialized</c> in subclasses that require the parent to
    /// function.
    /// </summary>
    protected TParent RequireParent()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"<{ShimName}> must be used inside a <{ParentName}>.");
        }
        return Parent;
    }
}
