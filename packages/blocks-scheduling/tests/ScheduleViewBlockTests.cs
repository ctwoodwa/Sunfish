using Sunfish.Blocks.Scheduling;
using Sunfish.Blocks.Scheduling.Models;
using Xunit;

namespace Sunfish.Blocks.Scheduling.Tests;

public class ScheduleViewBlockTests
{
    [Fact]
    public void ScheduleViewBlock_TypeIsPublicAndInBlocksSchedulingNamespace()
    {
        // ScheduleViewBlock is generic (<TResource>); grab the open generic type.
        var type = typeof(ScheduleViewBlock<>);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Scheduling", type.Namespace);
    }

    [Fact]
    public void ScheduleBlockView_HasFourCanonicalModes()
    {
        var values = System.Enum.GetNames<ScheduleBlockView>();
        Assert.Contains("Day", values);
        Assert.Contains("Week", values);
        Assert.Contains("Month", values);
        Assert.Contains("Allocation", values);
    }
}
