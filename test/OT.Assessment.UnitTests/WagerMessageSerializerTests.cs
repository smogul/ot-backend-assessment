using System.Text;
using System.Text.Json;
using OT.Assessment.Domain.Entities;
using OT.Assessment.Infrastructure.Messaging;

namespace OT.Assessment.UnitTests;

public class WagerMessageSerializerTests
{
    /// Fold a letter into an envelope and unfold it again: the words must match.
    [Fact]
    public void FromBytes_ReturnsTheSameWager_ThatWentIntoToBytes()
    {
        var original = TestWagers.Create();

        var copy = WagerMessageSerializer.FromBytes(WagerMessageSerializer.ToBytes(original));

        Assert.Equal(original.WagerId, copy.WagerId);
        Assert.Equal(original.Username, copy.Username);
        Assert.Equal(original.Amount, copy.Amount);
        Assert.Equal(original.CreatedDateTime, copy.CreatedDateTime);
        Assert.Equal(original.Duration, copy.Duration);
    }

    /// A letter that only says "hello" is not a betting slip.
    [Fact]
    public void FromBytes_Throws_WhenTheMessageIsNotAWager()
    {
        var notAWager = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");

        Assert.Throws<JsonException>(() => WagerMessageSerializer.FromBytes(notAWager));
    }
}
