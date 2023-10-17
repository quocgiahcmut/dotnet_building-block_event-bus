using Polly;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace EventBus.RabbitMQ;

public class EventBusRabbitMQ : IEventBus, IDisposable
{
    const string BROKER_NAME = "my_event_bus";

    private static readonly JsonSerializerOptions s_indentedOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions s_caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IRabbitMQPersistentConnection _persistentConnection;
    private readonly ILogger<EventBusRabbitMQ> _logger;
    private readonly IEventBusSubscriptionsManager _subscriptionsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _retryCount;

    private IModel _consumerChannel;
    private string _queueName;

    public EventBusRabbitMQ(IRabbitMQPersistentConnection persistentConnection,
        ILogger<EventBusRabbitMQ> logger,
        IEventBusSubscriptionsManager subscriptionsManager,
        IServiceProvider serviceProvider,
        string queueName,
        int retryCount = 5)
    {
        _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscriptionsManager = subscriptionsManager ?? new InMemoryEventBusSubscriptionsManager();
        _queueName = queueName;
        _consumerChannel = CreateConsumerChannel();
        _serviceProvider = serviceProvider;
        _retryCount = retryCount;
        _subscriptionsManager.OnEventRemoved += SubscriptionsManager_OnEventRemoved;
    }

    private void SubscriptionsManager_OnEventRemoved(object sender, string eventName)
    {
        if (!_persistentConnection.IsConnected)
        {
            _persistentConnection.TryConnect();
        }

        using var channel = _persistentConnection.CreateModel();
        channel.QueueUnbind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);

        if (_subscriptionsManager.IsEmpty)
        {
            _queueName = string.Empty;
            _consumerChannel.Close();
        }
    }

    public void Publish(IntegrationEvent @event)
    {
        if (!_persistentConnection.IsConnected)
        {
            _persistentConnection.TryConnect();
        }

        var policy = RetryPolicy.Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
            {
                _logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s", @event.Id, $"{time.TotalSeconds:n1}");
            });

        var eventName = @event.GetType().Name;

        _logger.LogTrace("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

        using var channel = _persistentConnection.CreateModel();
        _logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);

        channel.ExchangeDeclare(exchange: BROKER_NAME, type: ExchangeType.Direct);

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), s_indentedOptions);

        policy.Execute(() =>
        {
            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // persistent

            _logger.LogTrace("Publishing event to RabbitMQ: {EventId}", @event.Id);

            channel.BasicPublish(exchange: BROKER_NAME, routingKey: eventName, mandatory: true, basicProperties: properties, body: body);
        });
    }

    public void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var eventName = _subscriptionsManager.GetEventKey<T>();
        DoInternalSubscription(eventName);

        _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

        _subscriptionsManager.AddSubscription<T, TH>();
        StartBasicConsume();
    }

    private void DoInternalSubscription(string eventName)
    {
        var containsKey = _subscriptionsManager.HasSubscriptionsForEvent(eventName);
        if (!containsKey)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            _consumerChannel.QueueBind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);
        }
    }

    public void Unsubscribe<T, TH>() where TH : IIntegrationEventHandler<T> where T : IntegrationEvent 
    {
        var eventName = _subscriptionsManager.GetEventKey<T>();

        _logger.LogInformation("Unsubscribing from event {EventName}", eventName);

        _subscriptionsManager.RemoveSubscription<T, TH>();
    }

    public void Dispose()
    {
        if (_consumerChannel != null)
        {
            _consumerChannel.Dispose();
        }

        _subscriptionsManager.Clear();
    }

    #region private func
    private IModel CreateConsumerChannel()
    {
        if (!_persistentConnection.IsConnected)
        {
            _persistentConnection.TryConnect();
        }

        _logger.LogTrace("Creating RabbitMQ consumer channel");

        var channel = _persistentConnection.CreateModel();

        channel.ExchangeDeclare(exchange: BROKER_NAME, ExchangeType.Direct);
        channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        channel.CallbackException += (sender, ea) =>
        {
            _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");
            _consumerChannel.Dispose();
            _consumerChannel = CreateConsumerChannel();
            StartBasicConsume();
        };

        return channel;
    }

    private void StartBasicConsume()
    {
        _logger.LogTrace("Starting RabbitMQ basic consume");

        if (_consumerChannel != null)
        {
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.Received += Consumer_Received;

            _consumerChannel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
        }
        else
        {
            _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
        }
    }


    private async Task Consumer_Received(object sender, BasicDeliverEventArgs @event)
    {
        var eventName = @event.RoutingKey;
        var message = Encoding.UTF8.GetString(@event.Body.Span);

        try
        {
            if (message.ToLowerInvariant().Contains("throw-fake-exception"))
            {
                throw new InvalidOperationException($"Fake exception requested: \"{message}\"");
            }

            await ProcessEvent(eventName, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error Processing message \"{Message}\"", message);
        }
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        _logger.LogTrace("Processing RabbitMQ event: {EventName}", eventName);

        if (_subscriptionsManager.HasSubscriptionsForEvent(eventName))
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var subscriptions = _subscriptionsManager.GetHandlersForEvent(eventName);

            foreach (var subscription in subscriptions)
            {
                var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                if (handler == null) continue;
                var eventType = _subscriptionsManager.GetEventTypeByName(eventName);
                var integrationEvent = JsonSerializer.Deserialize(message, eventType, s_caseInsensitiveOptions);
                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                await Task.Yield();
                await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
            }
        }
        else
        {
            _logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
        }
    }
    #endregion
}