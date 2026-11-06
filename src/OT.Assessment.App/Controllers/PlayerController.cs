using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OT.Assessment.App.Requests;
using OT.Assessment.Application.Dtos;
using OT.Assessment.Application.Services;

namespace OT.Assessment.App.Controllers;

[ApiController]
[Route("api/player")]
public sealed class PlayerController(WagerService wagerService) : ControllerBase
{
    /// Like handing your clothes in at a dry cleaner. You get a ticket straight away, and the cleaning happens afterwards.
    [HttpPost("casinowager")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitCasinoWager(
        CasinoWagerRequest request,
        CancellationToken cancellationToken)
    {
        await wagerService.SubmitWagerAsync(request.ToDomain(), cancellationToken);

        return Accepted();
    }

    /// GET api/player/5ac75fec-23e9-27d1-b660-179eee70003d/casino?page=1&pageSize=10 returns that player's 10 newest wagers. A page size of 0 returns a 400.
    [HttpGet("{playerId:guid}/casino")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PlayerWagerDto>>> GetPlayerWagers(
        Guid playerId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return await wagerService.GetPlayerWagersAsync(playerId, page, pageSize, cancellationToken);
    }

    /// GET api/player/topSpenders?count=10 returns the top 10 players. A count of 0 or 5000 returns a 400.
    [HttpGet("topSpenders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TopSpenderDto>>> GetTopSpenders(
        [FromQuery, Range(1, 100)] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var topSpenders = await wagerService.GetTopSpendersAsync(count, cancellationToken);

        return Ok(topSpenders);
    }
}
