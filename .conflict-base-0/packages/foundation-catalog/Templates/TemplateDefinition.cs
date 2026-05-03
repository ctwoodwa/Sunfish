using System.Text.Json.Nodes;

namespace Sunfish.Foundation.Catalog.Templates;

/// <summary>
/// A user-authored artifact (form, checklist, report, …) expressed as a data
/// schema plus a UI schema. Stored as metadata; rendered by an adapter.
/// </summary>
/// <param name="Id">Stable identifier, typically a URI (e.g. <c>https://sunfish.io/schemas/pm/lease-renewal</c>).</param>
/// <param name="Version">Semantic version of this template.</param>
/// <param name="Kind">Template classification.</param>
/// <param name="DataSchema">JSON Schema 2020-12 document describing the data shape.</param>
/// <param name="UiSchema">Renderer-facing schema (e.g. JSONForms-style layout).</param>
/// <param name="BaseRef">Optional id@version reference when this template inherits from another.</param>
/// <param name="DisplayName">Optional human-readable label.</param>
/// <param name="Description">Optional long-form description.</param>
/// <param name="Locale">Optional BCP-47 locale tag.</param>
public sealed record TemplateDefinition(
    string Id,
    string Version,
    TemplateKind Kind,
    JsonNode DataSchema,
    JsonNode UiSchema,
    string? BaseRef = null,
    string? DisplayName = null,
    string? Description = null,
    string? Locale = null);
