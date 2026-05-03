using Sunfish.Blocks.Forms;
using Sunfish.Blocks.Forms.State;
using Xunit;

namespace Sunfish.Blocks.Forms.Tests;

public class FormBlockTests
{
    [Fact]
    public void FormBlock_TypeIsPublicAndInBlocksFormsNamespace()
    {
        var type = typeof(FormBlock<>);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Forms", type.Namespace);
    }

    [Fact]
    public void FormBlockState_TypeIsPublicAndInStateNamespace()
    {
        var type = typeof(FormBlockState);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Forms.State", type.Namespace);
    }
}
