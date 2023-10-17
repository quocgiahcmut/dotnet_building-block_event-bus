using EventBus.Interface;
using EventBus.RabbitMQ;
using TestService1.IntegrationEvents.EventHandling;
using TestService1.IntegrationEvents.Events;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddEventBus(config);

builder.Services.AddTransient<ItemChangeValueEventHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

var eventBus = app.Services.GetRequiredService<IEventBus>();

eventBus.Subscribe<ItemChangeValueEvent, ItemChangeValueEventHandler>();

app.Run();
