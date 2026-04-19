using System.Text.Json.Nodes;

namespace Sunfish.Foundation.Catalog.Templates;

/// <summary>
/// Tenant-authored customization layered on top of a base <see cref="TemplateDefinition"/>.
/// Patches are expressed in JSON Merge Patch (RFC 7396) form and applied by
/// <see cref="TemplateMerger"/>.
/// </summary>
/// <param name="Id">Stable identifier for this overlay.</param>
/// <param name="Version">Overlay version.</param>
/// <param name="BaseRef">Id (or <c>id@version</c>) of the base template this overlay targets.</param>
/// <param name="DataSchemaPatch">Optional RFC-7396 patch applied to the base data schema.</param>
/// <param name="UiSchemaPatch">Optional RFC-7396 patch applied to the base UI schema.</param>
public sealed record TenantTemplateOverlay(
    string Id,
    string Version,
    string BaseRef,
    JsonNode? DataSchemaPatch = null,
    JsonNode? UiSchemaPatch = null);
