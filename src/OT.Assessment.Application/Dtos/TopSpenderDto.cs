namespace OT.Assessment.Application.Dtos;

/// Like one row of a high score table: "Thabo, total spent R150".
public sealed record TopSpenderDto(
    Guid AccountId,
    string Username,
    decimal TotalAmountSpend);
