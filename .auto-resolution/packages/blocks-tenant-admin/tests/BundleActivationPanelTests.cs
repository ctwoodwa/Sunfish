using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.TenantAdmin.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;
using Xunit;

namespace Sunfish.Blocks.TenantAdmin.Tests;

public class BundleActivationPanelTests : BunitContext
{
    [Fact]
    public void BundleActivationPanel_RendersBundleNameAndVersion_FromCatalog()
    {
        var catalog = new BundleCatalog();
        catalog.Register(new BusinessCaseBundleManifest
        {
            Key = "sunfish.bundles.test",
            Name = "Test Bundle",
            Version = "1.2.3",
            EditionMappings = new Dictionary<string, IReadOnlyList<string>>
            {
                ["standard"] = new[] { "sunfish.blocks.tenant-admin" },
            },
        });

        Services.AddSingleton<IBundleCatalog>(catalog);
        Services.AddSingleton<ITenantAdminService, InMemoryTenantAdminService>();

        var cut = Render<BundleActivationPanel>(p => p
            .Add(c => c.TenantId, new TenantId("tenant-a")));

        cut.WaitForState(
            () => !cut.Markup.Contains("sf-bundle-activation-panel__loading"),
            TimeSpan.FromSeconds(5));

        Assert.Contains("Test Bundle", cut.Markup);
        Assert.Contains("1.2.3", cut.Markup);
        Assert.Contains("standard", cut.Markup);
    }
}
