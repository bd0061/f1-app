namespace F1Ticketing.Api.Services;

public sealed class ExchangeRateService(HttpClient httpClient) : IExchangeRateService
{
    // Osnovne cene su u EUR. Za drugu valutu pozivamo javni Frankfurter API
    // i dobijeni kurs koristimo za konačan obračun.
    public async Task<decimal> GetRateAsync(
        string from,
        string to,
        CancellationToken cancellationToken
    )
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
            return 1m;
        var result = await httpClient.GetFromJsonAsync<FrankfurterResponse>(
            $"https://api.frankfurter.app/latest?from={from}&to={to}",
            cancellationToken
        );
        return result?.Rates?.GetValueOrDefault(to)
            ?? throw new InvalidOperationException("Currency exchange rate unavailable.");
    }

    private sealed record FrankfurterResponse(Dictionary<string, decimal>? Rates);
}
