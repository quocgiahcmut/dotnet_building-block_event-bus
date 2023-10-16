using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventBus.Test;

public class RabbitMQ_Persistent_Connection_Tests
{
    [Fact]
    public void Create_Connection()
    {
        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "admin",
            Password = "admin123",
            VirtualHost = "/"
        };

        var loggerMock = new Mock<ILogger<DefaultRabbitMQPersistentConnection>>();

        var persistentConnection = new DefaultRabbitMQPersistentConnection(factory, loggerMock.Object);
        var loggerEBusMock = new Mock<ILogger<EventBusRabbitMQ>>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        string subscriptionClientName = "test_client";

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var eventBusRabbitMQ = new EventBusRabbitMQ(persistentConnection, loggerEBusMock.Object, subscriptionsManager, serviceProvider, subscriptionClientName);

        Assert.NotNull(eventBusRabbitMQ);
    }
}
