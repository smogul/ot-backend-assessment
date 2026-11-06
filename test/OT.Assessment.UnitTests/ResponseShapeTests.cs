using System.Globalization;
using System.Text.Json;
using OT.Assessment.Application.Dtos;
using OT.Assessment.Infrastructure.Persistence;

namespace OT.Assessment.UnitTests;

public class ResponseShapeTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    /// A renamed field or a missing Z would break the README's example response.
    [Fact]
    public void PlayerWager_UsesTheReadmeFieldNames()
    {
        var createdDate = new DateTime(2024, 9, 10, 7, 25, 13, DateTimeKind.Utc);
        var wager = new PlayerWagerDto(Guid.NewGuid(), "Lucky Fish", "Fun Games", 500m, createdDate);

        var json = JsonSerializer.Serialize(wager, WebOptions);

        Assert.Contains("\"game\":\"Lucky Fish\"", json);
        Assert.Contains("\"provider\":\"Fun Games\"", json);
        Assert.Contains("\"amount\":500", json);
        Assert.Contains("\"createdDate\":\"2024-09-10T07:25:13Z\"", json);
    }

    /// "totalAmountSpent" looks more correct, but it would not match the README.
    [Fact]
    public void TopSpender_UsesTheReadmeFieldNames()
    {
        var spender = new TopSpenderDto(Guid.NewGuid(), "Thabo", 150m);

        var json = JsonSerializer.Serialize(spender, WebOptions);

        Assert.Contains("\"username\":\"Thabo\"", json);
        Assert.Contains("\"totalAmountSpend\":150", json);
    }

    /// 500.000000000000000 must print as 500, and the long amount must keep every digit that matters.
    [Theory]
    [InlineData("500.000000000000000", "500")]
    [InlineData("38273.974454660885000", "38273.974454660885")]
    [InlineData("14.930832332433589", "14.930832332433589")]
    public void WithoutTrailingZeros_RemovesOnlyTheExtraZeros(string fromDatabase, string expected)
    {
        var value = decimal.Parse(fromDatabase, CultureInfo.InvariantCulture);

        var tidied = value.WithoutTrailingZeros();

        Assert.Equal(expected, tidied.ToString(CultureInfo.InvariantCulture));
    }
}
