namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// OpenFGA-style ReBAC permission evaluator. Interprets a <see cref="PolicyModel"/> against an
/// <see cref="IRelationTupleStore"/> to render <see cref="Decision"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Evaluation walks the <see cref="RelationRewrite"/> tree of the requested relation, resolving
/// direct-user tuples against the tuple store and recursing through computed-usersets,
/// tuple-to-usersets, and set-algebra combinators. See the spec §3.5 worked example.
/// </para>
/// <para>
/// <b>Action-to-relation mapping:</b> The evaluator maps conventional actions (<c>read</c>,
/// <c>write</c>, <c>delete</c>) to the matching <c>can_*</c> relation; arbitrary actions are
/// prefixed with <c>can_</c>. If a custom mapping is needed, wrap the evaluator.
/// </para>
/// <para>
/// <b>Cycle safety:</b> The recursion tracks the set of <c>(relation, resource)</c> frames already
/// in-flight. If a cycle is detected the evaluator treats the offending branch as <c>false</c>
/// rather than looping. Cycles in authored policy are a design smell but should not crash.
/// </para>
/// </remarks>
public sealed class ReBACPolicyEvaluator : IPermissionEvaluator
{
    private readonly PolicyModel _model;
    private readonly IRelationTupleStore _tuples;

    /// <summary>Creates an evaluator bound to a <paramref name="model"/> and <paramref name="tuples"/> store.</summary>
    public ReBACPolicyEvaluator(PolicyModel model, IRelationTupleStore tuples)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(tuples);
        _model = model;
        _tuples = tuples;
    }

    /// <summary>Maps an <see cref="ActionType"/> to a relation name.</summary>
    private static string MapActionToRelation(ActionType action) => action.Name switch
    {
        "read"   => "can_read",
        "write"  => "can_write",
        "delete" => "can_delete",
        _        => "can_" + action.Name,
    };

    /// <inheritdoc />
    public async ValueTask<Decision> EvaluateAsync(
        Subject subject,
        ActionType action,
        PolicyResource resource,
        ContextEnvelope context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        if (!_model.Types.TryGetValue(resource.TypeName, out var typeDef))
            return Decision.Indeterminate($"Unknown type '{resource.TypeName}' in policy model");

        var relationName = MapActionToRelation(action);
        if (!typeDef.Relations.ContainsKey(relationName))
            return Decision.Deny($"No relation '{relationName}' on type '{resource.TypeName}'");

        var visiting = new HashSet<(string Relation, PolicyResource Resource)>();
        var permitted = await EvaluateRelationAsync(relationName, subject, resource, context, visiting, ct);

        var matchedPolicy = $"{resource.TypeName}#{relationName}";
        return permitted
            ? Decision.Permit($"Permitted by {matchedPolicy}", matchedPolicy)
            : Decision.Deny($"No matching tuple for {matchedPolicy}");
    }

    /// <summary>
    /// Evaluates a named relation on a resource: looks up its <see cref="RelationRewrite"/> in the
    /// model and dispatches to <see cref="EvaluateRewriteAsync"/>. Missing relations are treated
    /// as <c>false</c> during recursion (the surface-level deny for unknown top-level relations is
    /// handled in <see cref="EvaluateAsync"/>).
    /// </summary>
    private async ValueTask<bool> EvaluateRelationAsync(
        string relation,
        Subject subject,
        PolicyResource resource,
        ContextEnvelope context,
        HashSet<(string Relation, PolicyResource Resource)> visiting,
        CancellationToken ct)
    {
        if (!_model.Types.TryGetValue(resource.TypeName, out var typeDef))
            return false;
        if (!typeDef.Relations.TryGetValue(relation, out var rewrite))
            return false;

        var frame = (relation, resource);
        if (!visiting.Add(frame))
            return false; // cycle: treat as no-match
        try
        {
            return await EvaluateRewriteAsync(rewrite, relation, subject, resource, context, visiting, ct);
        }
        finally
        {
            visiting.Remove(frame);
        }
    }

    private async ValueTask<bool> EvaluateRewriteAsync(
        RelationRewrite rewrite,
        string currentRelation,
        Subject subject,
        PolicyResource resource,
        ContextEnvelope context,
        HashSet<(string Relation, PolicyResource Resource)> visiting,
        CancellationToken ct)
    {
        switch (rewrite)
        {
            case RelationRewrite.Self:
            case RelationRewrite.DirectUsers:
            {
                // Direct tuple check with the current relation name.
                return await _tuples.SubjectHasRelationAsync(subject, currentRelation, resource, ct);
            }

            case RelationRewrite.ComputedUserset cu:
                return await EvaluateRelationAsync(cu.Relation, subject, resource, context, visiting, ct);

            case RelationRewrite.TupleToUserset ttu:
            {
                // Find all user-side entries u such that (u, Tupleset, resource) is in the store;
                // for each, resolve the ComputedRelation against u's resource view.
                await foreach (var u in _tuples.ListUsersAsync(ttu.Tupleset, resource, ct))
                {
                    switch (u)
                    {
                        case UsersetRef.User _:
                            // A principal can't serve as the target of another relation lookup in
                            // our model — principals aren't resources. Skip.
                            continue;
                        case UsersetRef.SelfRef self:
                            if (await EvaluateRelationAsync(ttu.ComputedRelation, subject, self.Resource, context, visiting, ct))
                                return true;
                            break;
                        case UsersetRef.Set setRef:
                            if (await EvaluateRelationAsync(setRef.Relation, subject, setRef.Resource, context, visiting, ct))
                                return true;
                            break;
                    }
                }
                return false;
            }

            case RelationRewrite.Union un:
            {
                foreach (var child in un.Children)
                {
                    if (await EvaluateRewriteAsync(child, currentRelation, subject, resource, context, visiting, ct))
                        return true;
                }
                return false;
            }

            case RelationRewrite.Intersection it:
            {
                if (it.Children.Count == 0) return false;
                foreach (var child in it.Children)
                {
                    if (!await EvaluateRewriteAsync(child, currentRelation, subject, resource, context, visiting, ct))
                        return false;
                }
                return true;
            }

            case RelationRewrite.Exclusion ex:
            {
                var include = await EvaluateRewriteAsync(ex.Include, currentRelation, subject, resource, context, visiting, ct);
                if (!include) return false;
                var exclude = await EvaluateRewriteAsync(ex.Exclude, currentRelation, subject, resource, context, visiting, ct);
                return !exclude;
            }
        }

        return false;
    }
}
