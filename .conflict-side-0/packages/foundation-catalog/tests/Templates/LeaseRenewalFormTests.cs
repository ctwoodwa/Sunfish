using Json.Schema;
using Sunfish.Foundation.Catalog.Templates;

namespace Sunfish.Foundation.Catalog.Tests.Templates;

/// <summary>
/// End-to-end exercise of the type-customization model from ADR 0005:
/// a canonical Sunfish-authored form template ships, a tenant overlay
/// patches it, the merged template validates tenant-specific submissions
/// that the base template would have rejected.
/// </summary>
public class LeaseRenewalFormTests
{
    private const string FixtureDir = "Fixtures/Templates";
    private const string BaseId = "https://sunfish.io/schemas/property-management/lease-renewal/1.0.0";
    private static readonly JsonSerializerOptions OverlayJsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Base_schema_accepts_a_valid_renewal_submission()
    {
        const string payload = """
        {
          "tenantId": "11111111-1111-1111-1111-111111111111",
          "currentLeaseEndDate": "2026-08-31",
          "renewalTermMonths": 12,
          "proposedRent": 1850.00,
          "notes": "Two years on time."
        }
        """;

        Assert.True(Validate(LoadFixture("lease-renewal.schema.json"), payload));
    }

    [Fact]
    public void Base_schema_rejects_a_renewal_term_beyond_the_canonical_cap()
    {
        const string payload = """
        {
          "tenantId": "11111111-1111-1111-1111-111111111111",
          "currentLeaseEndDate": "2026-08-31",
          "renewalTermMonths": 48,
          "proposedRent": 1850.00
        }
        """;

        Assert.False(Validate(LoadFixture("lease-renewal.schema.json"), payload));
    }

    [Fact]
    public void Tenant_overlay_widens_term_cap_and_adds_a_pet_deposit_field()
    {
        var resolved = ResolveBaseWithOverlay();
        const string payload = """
        {
          "tenantId": "11111111-1111-1111-1111-111111111111",
          "currentLeaseEndDate": "2026-08-31",
          "renewalTermMonths": 48,
          "proposedRent": 1850.00,
          "petDeposit": 350.00
        }
        """;

        Assert.True(Validate(resolved.DataSchema.ToJsonString(), payload));
    }

    [Fact]
    public void Tenant_overlay_preserves_base_required_fields()
    {
        var resolved = ResolveBaseWithOverlay();
        // Omits tenantId, which is required by the base and not removed by the overlay.
        const string payload = """
        {
          "currentLeaseEndDate": "2026-08-31",
          "renewalTermMonths": 12,
          "proposedRent": 1850.00
        }
        """;

        Assert.False(Validate(resolved.DataSchema.ToJsonString(), payload));
    }

    [Fact]
    public void Tenant_overlay_applies_ui_schema_label_override()
    {
        var resolved = ResolveBaseWithOverlay();

        Assert.Equal("Acme Lease Renewal", resolved.UiSchema["label"]!.GetValue<string>());
    }

    private static TemplateDefinition ResolveBaseWithOverlay()
    {
        var baseDefinition = new TemplateDefinition(
            Id: BaseId,
            Version: "1.0.0",
            Kind: TemplateKind.Form,
            DataSchema: JsonNode.Parse(LoadFixture("lease-renewal.schema.json"))!,
            UiSchema: JsonNode.Parse(LoadFixture("lease-renewal.uischema.json"))!,
            DisplayName: "Lease Renewal");

        var overlay = JsonSerializer.Deserialize<TenantTemplateOverlay>(
            LoadFixture("lease-renewal.overlay.json"),
            OverlayJsonOptions)
            ?? throw new InvalidOperationException("Overlay fixture failed to deserialize.");

        return TemplateMerger.Resolve(baseDefinition, overlay);
    }

    // Strip $id to avoid colliding with JsonSchema.Net's global registry across test runs;
    // evaluation does not require $id.
    private static bool Validate(string schemaJson, string payloadJson)
    {
        var node = JsonNode.Parse(schemaJson)!.AsObject();
        node.Remove("$id");
        var schema = JsonSchema.FromText(node.ToJsonString());
        using var doc = JsonDocument.Parse(payloadJson);
        return schema.Evaluate(doc.RootElement).IsValid;
    }

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, FixtureDir, name);
        return File.ReadAllText(path);
    }
}
