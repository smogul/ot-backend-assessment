namespace OT.Assessment.Infrastructure.Persistence;

public static class DecimalExtensions
{
    /// 500.000000000000000 becomes 500. 38273.974454660885000 becomes 38273.974454660885.
    public static decimal WithoutTrailingZeros(this decimal value)
    {
        return value / 1.000000000000000000000000000000000m;
    }
}
