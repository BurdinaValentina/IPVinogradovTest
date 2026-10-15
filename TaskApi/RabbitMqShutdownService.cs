using Microsoft.Extensions.Hosting;

namespace TaskApi;

public class RabbitMqShutdownService : IHostedService
{
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<RabbitMqShutdownService> _logger;

    public RabbitMqShutdownService(IRabbitMqPublisher publisher, ILogger<RabbitMqShutdownService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Graceful shutdown: закрываем RabbitMQ...");
        if (_publisher is IDisposable disposable)
        {
            disposable.Dispose();
        }
        return Task.CompletedTask;
    }
}