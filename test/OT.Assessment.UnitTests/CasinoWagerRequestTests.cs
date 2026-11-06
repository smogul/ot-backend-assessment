using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using OT.Assessment.App.Requests;

namespace OT.Assessment.UnitTests;

public class CasinoWagerRequestTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    /// Passing "ZA" gives a wager from South Africa. Passing "South Africa" gives a wager with a country code that is too long, for testing rejection.
    private static string TesterJson(string countryCode = "ZA", string amount = "38273.974454660885")
    {
        return $$"""
            {
              "wagerId": "aa6700eb-1a06-483e-9739-d293dc7a9383",
              "theme": "adventure",
              "provider": "Fun Games",
              "gameName": "Lucky Fish",
              "transactionId": "410b7161-3473-4d74-85c3-a533d050a9d3",
              "brandId": "8a2016f8-c4c4-471f-9a9c-337a54664650",
              "accountId": "5ac75fec-23e9-27d1-b660-179eee70003d",
              "Username": "Thabo",
              "externalReferenceId": "0267dbca-2760-4a9e-ab42-5ce766fa8ca0",
              "transactionTypeId": "8aaece0c-5d53-4225-a937-adb454c4da31",
              "amount": {{amount}},
              "createdDateTime": "2024-05-04T02:25:05.9906387+02:00",
              "numberOfBets": 3,
              "countryCode": "{{countryCode}}",
              "sessionData": "this field is not stored",
              "Duration": 1827254
            }
            """;
    }

    /// "Username": "Thabo" must become Username = "Thabo", not a blank name.
    [Fact]
    public void ToDomain_ReadsCapitalisedFieldNames_FromTheTester()
    {
        var request = JsonSerializer.Deserialize<CasinoWagerRequest>(TesterJson(), WebOptions)!;

        var wager = request.ToDomain();

        Assert.Equal("Thabo", wager.Username);
        Assert.Equal(1827254, wager.Duration);
    }

    /// 38273.974454660885 must not be rounded, and 02:25 at +02:00 is 00:25 UTC.
    [Fact]
    public void ToDomain_KeepsTheExactAmountAndTime()
    {
        var request = JsonSerializer.Deserialize<CasinoWagerRequest>(TesterJson(), WebOptions)!;

        var wager = request.ToDomain();

        Assert.Equal(38273.974454660885m, wager.Amount);
        Assert.Equal(DateTimeOffset.Parse("2024-05-04T00:25:05.9906387Z"), wager.CreatedDateTime);
    }

    /// Like a betting slip with no number on it. The cashier hands it back.
    [Fact]
    public void Deserialize_Throws_WhenARequiredFieldIsMissing()
    {
        var json = TesterJson().Replace("\"wagerId\": \"aa6700eb-1a06-483e-9739-d293dc7a9383\",", string.Empty);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CasinoWagerRequest>(json, WebOptions));
    }

    /// A normal wager with the country code "ZA" should have no problems.
    [Fact]
    public void Validation_Passes_ForTesterData()
    {
        var request = JsonSerializer.Deserialize<CasinoWagerRequest>(TesterJson(), WebOptions)!;

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);

        Assert.True(isValid);
    }

    /// "South Africa" is too long, so this wager is rejected.
    [Fact]
    public void Validation_Fails_WhenCountryCodeIsTooLong()
    {
        var request = JsonSerializer.Deserialize<CasinoWagerRequest>(TesterJson("South Africa"), WebOptions)!;

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);

        Assert.False(isValid);
    }

    /// A parcel too big for the shelves is refused at reception, so it does not block the warehouse. 9999999999999 still fits, and one more digit does not.
    [Theory]
    [InlineData("9999999999999", true)]
    [InlineData("100000000000000", false)]
    public void Validation_RejectsAmountsTooBigForTheDatabase(string amount, bool expected)
    {
        var request = JsonSerializer.Deserialize<CasinoWagerRequest>(TesterJson(amount: amount), WebOptions)!;

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);

        Assert.Equal(expected, isValid);
    }
}
