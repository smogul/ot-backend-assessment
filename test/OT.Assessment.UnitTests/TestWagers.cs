using OT.Assessment.Domain.Entities;

namespace OT.Assessment.UnitTests;

internal static class TestWagers
{
    /// Gives you Thabo's bet on "Lucky Fish", so each test can skip writing out all 15 fields.
    public static CasinoWager Create()
    {
        return new CasinoWager
        {
            WagerId = Guid.NewGuid(),
            Theme = "adventure",
            Provider = "Fun Games",
            GameName = "Lucky Fish",
            TransactionId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Username = "Thabo",
            ExternalReferenceId = Guid.NewGuid(),
            TransactionTypeId = Guid.NewGuid(),
            Amount = 38273.974454660885m,
            CreatedDateTime = new DateTimeOffset(2024, 5, 4, 2, 25, 5, TimeSpan.FromHours(2)),
            NumberOfBets = 3,
            CountryCode = "ZA",
            Duration = 1827254
        };
    }
}
