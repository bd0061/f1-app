namespace F1Ticketing.Api.Services;

public interface IExchangeRateService
{
    Task<decimal> GetRateAsync(string from, string to, CancellationToken cancellationToken);
}
