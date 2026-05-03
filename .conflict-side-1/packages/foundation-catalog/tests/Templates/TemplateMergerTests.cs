using Sunfish.Foundation.Catalog.Templates;

namespace Sunfish.Foundation.Catalog.Tests.Templates;

public class TemplateMergerTests
{
    [Fact]
    public void ApplyMergePatch_replaces_primitive_target_with_patch_value()
    {
        var target = JsonNode.Parse("""{"a": 1}""")!;
        var patch = JsonNode.Parse("""{"a": 2}""")!;

        var result = TemplateMerger.ApplyMergePatch(target, patch);

        Assert.Equal(2, result!["a"]!.GetValue<int>());
    }

    [Fact]
    public void ApplyMergePatch_merges_nested_objects_recursively()
    {
        var target = JsonNode.Parse("""{"nested": {"a": 1, "b": 2}}""")!;
        var patch = JsonNode.Parse("""{"nested": {"b": 99, "c": 3}}""")!;

        var result = TemplateMerger.ApplyMergePatch(target, patch);

        Assert.Equal(1, result!["nested"]!["a"]!.GetValue<int>());
        Assert.Equal(99, result["nested"]!["b"]!.GetValue<int>());
        Assert.Equal(3, result["nested"]!["c"]!.GetValue<int>());
    }

    [Fact]
    public void ApplyMergePatch_null_value_removes_key()
    {
        var target = JsonNode.Parse("""{"a": 1, "b": 2}""")!;
        var patch = JsonNode.Parse("""{"a": null}""")!;

        var result = TemplateMerger.ApplyMergePatch(target, patch);

        Assert.False(result!.AsObject().ContainsKey("a"));
        Assert.Equal(2, result["b"]!.GetValue<int>());
    }

    [Fact]
    public void ApplyMergePatch_non_object_patch_replaces_target_wholesale()
    {
        var target = JsonNode.Parse("""{"a": 1}""")!;
        var patch = JsonNode.Parse("""[1, 2, 3]""")!;

        var result = TemplateMerger.ApplyMergePatch(target, patch);

        Assert.IsAssignableFrom<JsonArray>(result);
        Assert.Equal(3, ((JsonArray)result!).Count);
    }

    [Fact]
    public void ApplyMergePatch_does_not_mutate_inputs()
    {
        var targetJson = """{"a": {"b": 1}}""";
        var patchJson = """{"a": {"b": 9, "c": 3}}""";
        var target = JsonNode.Parse(targetJson)!;
        var patch = JsonNode.Parse(patchJson)!;

        _ = TemplateMerger.ApplyMergePatch(target, patch);

        Assert.Equal(1, target["a"]!["b"]!.GetValue<int>());
        Assert.False(target["a"]!.AsObject().ContainsKey("c"));
        Assert.Equal(9, patch["a"]!["b"]!.GetValue<int>());
    }

    [Fact]
    public void Resolve_rejects_overlay_whose_base_ref_does_not_match()
    {
        var baseDef = NewBase("sunfish://x", "1.0.0");
        var overlay = new TenantTemplateOverlay(
            Id: "tenant://acme/x",
            Version: "1.0.0",
            BaseRef: "sunfish://y",
            DataSchemaPatch: JsonNode.Parse("""{"title":"overridden"}"""));

        Assert.Throws<InvalidOperationException>(
            () => TemplateMerger.Resolve(baseDef, overlay));
    }

    [Fact]
    public void Resolve_applies_patches_to_both_schemas()
    {
        var baseDef = NewBase("sunfish://form", "1.0.0");
        var overlay = new TenantTemplateOverlay(
            Id: "tenant://acme/form",
            Version: "1.0.0",
            BaseRef: "sunfish://form",
            DataSchemaPatch: JsonNode.Parse("""{"title":"Acme Form"}"""),
            UiSchemaPatch: JsonNode.Parse("""{"label":"Acme"}"""));

        var resolved = TemplateMerger.Resolve(baseDef, overlay);

        Assert.Equal("Acme Form", resolved.DataSchema["title"]!.GetValue<string>());
        Assert.Equal("Acme", resolved.UiSchema["label"]!.GetValue<string>());
    }

    [Fact]
    public void Resolve_accepts_versioned_base_ref()
    {
        var baseDef = NewBase("sunfish://form", "1.0.0");
        var overlay = new TenantTemplateOverlay(
            Id: "tenant://acme/form",
            Version: "1.0.0",
            BaseRef: "sunfish://form@1.0.0");

        var resolved = TemplateMerger.Resolve(baseDef, overlay);

        Assert.Equal("sunfish://form", resolved.Id);
    }

    private static TemplateDefinition NewBase(string id, string version) => new(
        Id: id,
        Version: version,
        Kind: TemplateKind.Form,
        DataSchema: JsonNode.Parse("""{"title":"Base","type":"object"}""")!,
        UiSchema: JsonNode.Parse("""{"type":"VerticalLayout"}""")!);
}
