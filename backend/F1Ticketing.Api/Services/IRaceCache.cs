namespace F1Ticketing.Api.Services;

public interface IRaceCache
{
    Task<string?> GetAsync();
    Task SetAsync(string value);
    Task InvalidateAsync();
}
