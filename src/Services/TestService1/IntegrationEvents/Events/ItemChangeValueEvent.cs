using EventBus.Models;

namespace TestService1.IntegrationEvents.Events;

public record ItemChangeValueEvent : IntegrationEvent
{
    public int ItemId { get; private init; }
    public int NewValue { get; private init; }
    public int OldValue { get; private init; }

    public ItemChangeValueEvent(int itemId, int newValue, int oldValue)
    {
        ItemId = itemId;
        NewValue = newValue;
        OldValue = oldValue;
    }
}
