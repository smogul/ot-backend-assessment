using System.Text.Json;
using OT.Assessment.Domain.Entities;

namespace OT.Assessment.Infrastructure.Messaging;

public static class WagerMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// Like folding a letter so it fits into an envelope. Thabo's R50 wager becomes bytes holding text like {"username":"Thabo","amount":50}.
    public static byte[] ToBytes(CasinoWager wager)
    {
        return JsonSerializer.SerializeToUtf8Bytes(wager, Options);
    }

    /// Like unfolding the letter and reading it. If the envelope holds a torn page with half the words missing, we say so rather than guessing.
    public static CasinoWager FromBytes(byte[] messageBody)
    {
        return JsonSerializer.Deserialize<CasinoWager>(messageBody, Options)
            ?? throw new JsonException("The message was empty, so it is not a wager.");
    }
}
