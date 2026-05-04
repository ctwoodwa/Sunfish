using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Schema descriptor attached to every <see cref="AtlasSettingSnapshot"/>.
/// Per ADR 0065 §5 — describes how the Atlas form view should render and
/// validate this setting. Phase 3b adds the
/// <c>Sunfish.Wayfinder.Analyzers.SchemaRegistrationAnalyzer</c> Roslyn check
/// that enforces every settable path has a registered descriptor.
/// </summary>
/// <param name="JsonSchema">RFC draft 2020-12 JSON Schema describing the value's shape and validation rules.</param>
/// <param name="DisplayName">Human-readable display name for the Atlas form-view header.</param>
/// <param name="DescriptionMarkdown">Operator-facing description; rendered as Markdown in the Atlas form view.</param>
/// <param name="Kind">Type discriminator; the form view uses this to pick a renderer.</param>
public sealed record AtlasSchemaDescriptor(
    [property: JsonPropertyName("jsonSchema")] JsonNode JsonSchema,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("descriptionMarkdown")] string DescriptionMarkdown,
    [property: JsonPropertyName("kind")] AtlasSettingKind Kind);
