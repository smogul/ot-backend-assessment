namespace OT.Assessment.Application.Dtos;

/// Like one line on a shop receipt: "Lucky Fish, Fun Games, R50, 10 Sept".
public sealed record PlayerWagerDto(
    Guid WagerId,
    string Game,
    string Provider,
    decimal Amount,
    DateTime CreatedDate);
