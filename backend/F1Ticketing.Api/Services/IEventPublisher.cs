namespace F1Ticketing.Api.Services;

public interface IEventPublisher
{
    Task PublishAsync(
        string eventName,
        object payload,
        CancellationToken cancellationToken = default
    );
}
