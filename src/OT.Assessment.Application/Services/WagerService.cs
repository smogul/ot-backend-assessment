using OT.Assessment.Application.Abstractions;
using OT.Assessment.Application.Dtos;
using OT.Assessment.Domain.Entities;

namespace OT.Assessment.Application.Services;

public sealed class WagerService(IWagerPublisher publisher, IWagerReader reader)
{
    /// Thabo bets R50. His bet joins the line, and the consumer saves it later.
    public Task SubmitWagerAsync(CasinoWager wager, CancellationToken cancellationToken)
    {
        return publisher.PublishAsync(wager, cancellationToken);
    }

    /// Asking for Thabo with page 1 and page size 10 gives his 10 newest wagers.
    public Task<PagedResult<PlayerWagerDto>> GetPlayerWagersAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return reader.GetPlayerWagersAsync(accountId, page, pageSize, cancellationToken);
    }

    /// Asking for a count of 3 gives the top 3 players, like a podium.
    public Task<IReadOnlyList<TopSpenderDto>> GetTopSpendersAsync(
        int count,
        CancellationToken cancellationToken)
    {
        return reader.GetTopSpendersAsync(count, cancellationToken);
    }
}
