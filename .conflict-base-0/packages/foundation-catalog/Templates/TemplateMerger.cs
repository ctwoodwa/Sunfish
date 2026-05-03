using System.Text.Json.Nodes;

namespace Sunfish.Foundation.Catalog.Templates;

/// <summary>
/// Applies a tenant overlay to a base template using JSON Merge Patch
/// (RFC 7396). Does not mutate its inputs.
/// </summary>
public static class TemplateMerger
{
    /// <summary>
    /// Resolves a base template and an overlay into a merged
    /// <see cref="TemplateDefinition"/>. Throws if the overlay's
    /// <see cref="TenantTemplateOverlay.BaseRef"/> does not match the base
    /// template's id or <c>id@version</c>.
    /// </summary>
    public static TemplateDefinition Resolve(TemplateDefinition baseDefinition, TenantTemplateOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(baseDefinition);
        ArgumentNullException.ThrowIfNull(overlay);

        var versioned = $"{baseDefinition.Id}@{baseDefinition.Version}";
        if (!string.Equals(overlay.BaseRef, baseDefinition.Id, StringComparison.Ordinal)
            && !string.Equals(overlay.BaseRef, versioned, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Overlay base ref '{overlay.BaseRef}' does not match base template '{versioned}'.");
        }

        var data = overlay.DataSchemaPatch is null
            ? baseDefinition.DataSchema.DeepClone()
            : ApplyMergePatch(baseDefinition.DataSchema, overlay.DataSchemaPatch)
                ?? throw new InvalidOperationException("Data schema patch produced a null result.");

        var ui = overlay.UiSchemaPatch is null
            ? baseDefinition.UiSchema.DeepClone()
            : ApplyMergePatch(baseDefinition.UiSchema, overlay.UiSchemaPatch)
                ?? throw new InvalidOperationException("UI schema patch produced a null result.");

        return baseDefinition with { DataSchema = data, UiSchema = ui };
    }

    /// <summary>
    /// Applies a JSON Merge Patch to a target document and returns a fresh
    /// result tree. Inputs are not mutated. Semantics follow RFC 7396:
    /// object-in-object patches merge recursively, null values in a patch
    /// object remove the corresponding target key, and any non-object patch
    /// replaces the target wholesale.
    /// </summary>
    public static JsonNode? ApplyMergePatch(JsonNode? target, JsonNode? patch)
    {
        if (patch is not JsonObject patchObject)
        {
            return patch?.DeepClone();
        }

        var result = new JsonObject();
        if (target is JsonObject targetObject)
        {
            foreach (var kvp in targetObject)
            {
                result[kvp.Key] = kvp.Value?.DeepClone();
            }
        }

        foreach (var kvp in patchObject)
        {
            if (kvp.Value is null)
            {
                result.Remove(kvp.Key);
            }
            else
            {
                result[kvp.Key] = ApplyMergePatch(result[kvp.Key], kvp.Value);
            }
        }

        return result;
    }
}
