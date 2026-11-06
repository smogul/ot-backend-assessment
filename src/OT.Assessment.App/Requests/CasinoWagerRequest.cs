using System.ComponentModel.DataAnnotations;
using OT.Assessment.Domain.Entities;

namespace OT.Assessment.App.Requests;

public sealed class CasinoWagerRequest
{
    public required Guid WagerId { get; init; }

    [StringLength(50)]
    public required string Theme { get; init; }

    [StringLength(100)]
    public required string Provider { get; init; }

    [StringLength(100)]
    public required string GameName { get; init; }

    public required Guid TransactionId { get; init; }

    public required Guid BrandId { get; init; }

    public required Guid AccountId { get; init; }

    [StringLength(100)]
    public required string Username { get; init; }

    public required Guid ExternalReferenceId { get; init; }

    public required Guid TransactionTypeId { get; init; }

    [Range(0.01, 9999999999999)]
    public required decimal Amount { get; init; }

    public required DateTimeOffset CreatedDateTime { get; init; }

    [Range(1, int.MaxValue)]
    public required int NumberOfBets { get; init; }

    [StringLength(2, MinimumLength = 2)]
    public required string CountryCode { get; init; }

    [Range(0, double.MaxValue)]
    public required long Duration { get; init; }

    /// Like a receptionist copying a visitor's form into the official school register. The details are the same, and only the form has changed.
    public CasinoWager ToDomain()
    {
        return new CasinoWager
        {
            WagerId = WagerId,
            Theme = Theme,
            Provider = Provider,
            GameName = GameName,
            TransactionId = TransactionId,
            BrandId = BrandId,
            AccountId = AccountId,
            Username = Username,
            ExternalReferenceId = ExternalReferenceId,
            TransactionTypeId = TransactionTypeId,
            Amount = Amount,
            CreatedDateTime = CreatedDateTime,
            NumberOfBets = NumberOfBets,
            CountryCode = CountryCode,
            Duration = Duration
        };
    }
}
