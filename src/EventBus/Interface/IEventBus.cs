namespace EventBus.Interface;

public interface IEventBus
{
    void Publish(IntegrationEvent @event);
    void Subscribe<T, TH>() where T : IntegrationEvent;
    void Unsubscribe<T, TH>() where T: IntegrationEvent;
}
