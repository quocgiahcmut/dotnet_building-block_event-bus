global using EventBus.Interface;
global using EventBus.Core;
global using EventBus.Models;
global using EventBus.Extensions;

global using RabbitMQ.Client;
global using RabbitMQ.Client.Events;

global using System.Text;
global using System.Text.Json;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.DependencyInjection;

global using Polly.Retry;