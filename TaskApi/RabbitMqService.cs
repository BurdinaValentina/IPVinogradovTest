using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace TaskApi;

public interface IRabbitMqPublisher
{
    Task PublishAsync(TaskCompletedEvent evt, CancellationToken ct);
}

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly string _connectionString;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new object();

    public RabbitMqPublisher(ILogger<RabbitMqPublisher> logger, IConfiguration config)
    {
        _logger = logger;
        _connectionString = config.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@127.0.0.1:5672";
    }

    private void EnsureConnected()
    {
        if (_connection == null || !_connection.IsOpen)
        {
            lock (_lock)
            {
                if (_connection == null || !_connection.IsOpen)
                {
                    var factory = new ConnectionFactory
                    {
                        Uri = new Uri(_connectionString),
                        DispatchConsumersAsync = true
                    };

                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();

                    _channel.ExchangeDeclare(exchange: "task.events", type: ExchangeType.Topic, durable: true);
                }
            }
        }
    }

    public Task PublishAsync(TaskCompletedEvent evt, CancellationToken ct)
    {
        try
        {
            EnsureConnected();
            if (_channel == null) return Task.CompletedTask;

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;

            _channel.BasicPublish(exchange: "task.events", routingKey: "task.completed", mandatory: false, basicProperties: properties, body: body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось отправить событие в RabbitMQ для задачи {TaskId}", evt.TaskId);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
        catch { }
    }
}