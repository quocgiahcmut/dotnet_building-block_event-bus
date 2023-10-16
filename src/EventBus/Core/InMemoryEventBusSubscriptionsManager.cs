﻿namespace EventBus.Core;

public partial class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
{
    private readonly IDictionary<string, List<SubscriptionInfo>> _handlers;
    private readonly List<Type> _eventTypes;

    public event EventHandler<string> OnEventRemoved;

    public InMemoryEventBusSubscriptionsManager()
    {
        _handlers = new Dictionary<string, List<SubscriptionInfo>>();
        _eventTypes = new List<Type>();
    }


    public bool IsEmpty => _handlers is { Count: 0 }; // Match pattern in .Net. It same _handlers.Count == 0
    public void Clear() => _handlers.Clear();

    public void AddSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var eventName = GetEventKey<T>();

        DoAddSubscription(typeof(TH), eventName);

        if (!_eventTypes.Contains(typeof(T)))
        {
            _eventTypes.Add(typeof(T));
        }
    }

    private void DoAddSubscription(Type handlerType, string eventName)
    {
        if (!HasSubscriptionsForEvent(eventName))
        {
            _handlers.Add(eventName, new List<SubscriptionInfo>());
        }

        if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
        {
            throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
        }

        _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
    }

    public void RemoveSubscription<T, TH>() where TH : IIntegrationEventHandler<T> where T : IntegrationEvent
    {
        var handlerToRemove = FindSubscriptionToRemove<T, TH>();
        var eventName = GetEventKey<T>();
        DoRemoveHandler(eventName, handlerToRemove);
    }

    private void DoRemoveHandler(string eventName, SubscriptionInfo subsToRemove)
    {
        if (subsToRemove != null)
        {
            _handlers[eventName].Remove(subsToRemove);
            if (!_handlers[eventName].Any())
            {
                _handlers.Remove(eventName);
                var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                if (eventType != null)
                {
                    _eventTypes.Remove(eventType);
                }
                RaiseOnEventRemoved(eventName);
            }
        }
    }

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
    {
        var eventName = GetEventKey<T>();
        return GetHandlersForEvent(eventName);
    }

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName) => _handlers[eventName];

    #region extension func
    private void RaiseOnEventRemoved(string eventName)
    {
        var handler = OnEventRemoved;
        handler?.Invoke(this, eventName);
    }

    private SubscriptionInfo FindSubscriptionToRemove<T, TH>() where T : IntegrationEvent where TH: IIntegrationEventHandler<T>
    {
        var eventName = GetEventKey<T>();

        if (!HasSubscriptionsForEvent(eventName))
        {
            return null;
        }

        return _handlers[eventName].SingleOrDefault(s => s.HandlerType == typeof(TH));
    }

    public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
    {
        var key = GetEventKey<T>();
        return HasSubscriptionsForEvent(key);
    }

    public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);

    public Type GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(t => t.Name == eventName);

    public string GetEventKey<T>()
    {
        return typeof(T).Name;
    }
    #endregion
}