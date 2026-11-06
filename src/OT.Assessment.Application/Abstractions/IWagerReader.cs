using OT.Assessment.Application.Dtos;

namespace OT.Assessment.Application.Abstractions;

public interface IWagerReader
{
    /// Think of a photo album. If Thabo has 35 wagers, page 1 with a page size of 10 shows his 10 newest, and page 2 shows the next 10.
    Task<PagedResult<PlayerWagerDto>> GetPlayerWagersAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// Think of a game's high score table. If Thabo spent R150 and Lerato spent R100, then asking for the top 2 gives Thabo first and Lerato second.
    Task<IReadOnlyList<TopSpenderDto>> GetTopSpendersAsync(
        int count,
        CancellationToken cancellationToken);
}
