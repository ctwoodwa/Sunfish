using System.Text.Json;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Validation;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class AttachmentStatusTransitionTests
{
    [Theory]
    [InlineData(AttachmentStatus.Active,     AttachmentStatus.Superseded)]
    [InlineData(AttachmentStatus.Active,     AttachmentStatus.Tombstoned)]
    [InlineData(AttachmentStatus.Superseded, AttachmentStatus.Tombstoned)]
    public void IsAllowed_AllowedTransitions_ReturnTrue(AttachmentStatus from, AttachmentStatus to)
    {
        Assert.True(AttachmentStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(AttachmentStatus.Superseded, AttachmentStatus.Active)]  // no un-supersede
    [InlineData(AttachmentStatus.Tombstoned, AttachmentStatus.Active)]  // no resurrection
    [InlineData(AttachmentStatus.Tombstoned, AttachmentStatus.Superseded)]
    public void IsAllowed_ForbiddenTransitions_ReturnFalse(AttachmentStatus from, AttachmentStatus to)
    {
        Assert.False(AttachmentStatusTransitions.IsAllowed(from, to));
    }

    [Fact]
    public void IsAllowed_TombstonedIsTerminal_NoOutgoing()
    {
        foreach (var to in Enum.GetValues<AttachmentStatus>())
            Assert.False(AttachmentStatusTransitions.IsAllowed(AttachmentStatus.Tombstoned, to));
    }

    [Fact]
    public void IsAllowed_SameStateIsNotASelfTransition()
    {
        foreach (var s in Enum.GetValues<AttachmentStatus>())
            Assert.False(AttachmentStatusTransitions.IsAllowed(s, s));
    }

    [Fact]
    public void AttachmentStatus_JsonRoundtrip_LowercaseCodes()
    {
        Assert.Equal("\"active\"",     JsonSerializer.Serialize(AttachmentStatus.Active));
        Assert.Equal("\"superseded\"", JsonSerializer.Serialize(AttachmentStatus.Superseded));
        Assert.Equal("\"tombstoned\"", JsonSerializer.Serialize(AttachmentStatus.Tombstoned));

        Assert.Equal(AttachmentStatus.Superseded, JsonSerializer.Deserialize<AttachmentStatus>("\"superseded\""));
    }

    [Fact]
    public void AttachmentStatus_UnknownJson_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AttachmentStatus>("\"draft\""));
    }
}
