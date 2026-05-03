using Sunfish.Blocks.Assets;
using Sunfish.Blocks.Assets.Models;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests;

public class AssetCatalogBlockTests
{
    [Fact]
    public void AssetCatalogBlock_TypeIsPublicAndInBlocksAssetsNamespace()
    {
        var type = typeof(AssetCatalogBlock);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Assets", type.Namespace);
    }

    [Fact]
    public void AssetRecord_TypeIsPublicAndInModelsNamespace()
    {
        var type = typeof(AssetRecord);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Assets.Models", type.Namespace);
    }
}
