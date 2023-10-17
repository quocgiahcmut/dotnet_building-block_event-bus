using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace EventBus.RabbitMQ;

public static class Extensions
{
    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        var eventBusSection = configuration.GetSection("EventBus");

        if (!eventBusSection.Exists())
        {
            return services;
        }

        services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

        if (string.Equals(eventBusSection["ProviderName"], "RabbitMQ", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();

                var factory = new ConnectionFactory()
                {
                    HostName = eventBusSection["HostName"],
                    DispatchConsumersAsync = true
                };

                if (!string.IsNullOrEmpty(eventBusSection["Port"]))
                {
                    factory.Port = Convert.ToInt32(eventBusSection["Port"]);
                }

                if (!string.IsNullOrEmpty(eventBusSection["UserName"]))
                {
                    factory.UserName = eventBusSection["UserName"];
                }

                if (!string.IsNullOrEmpty(eventBusSection["Password"]))
                {
                    factory.Password = eventBusSection["Password"];
                }

                var retryCount = Convert.ToInt32(eventBusSection["RetryCount"]);

                return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
            });

            services.AddSingleton<IEventBus, EventBusRabbitMQ>(sp =>
            {
                var subcriptionClientName = eventBusSection["SubscriptionClientName"];
                var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ>>();
                var eventBusSubscriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                var retryCount = Convert.ToInt32(eventBusSection["RetryCount"]);

                return new EventBusRabbitMQ(rabbitMQPersistentConnection, logger, eventBusSubscriptionsManager, sp, subcriptionClientName, retryCount);
            });
        }

        return services;
    }
}
