using EventBus.Interface;
using TestService1.IntegrationEvents.Events;

namespace TestService1.IntegrationEvents.EventHandling;

public class ItemChangeValueEventHandler : IIntegrationEventHandler<ItemChangeValueEvent>
{
    private readonly ILogger<ItemChangeValueEventHandler> _logger;

    public ItemChangeValueEventHandler(ILogger<ItemChangeValueEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ItemChangeValueEvent @event)
    {
        _logger.LogInformation($"{@event.ItemId}: {@event.OldValue} => {@event.NewValue}");

        return Task.CompletedTask;
    }
}
