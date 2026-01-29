using Microsoft.Extensions.Logging;
using SmartArchivist.Contract.Abstractions.Messaging;
using RabbitMQ.Client;

namespace SmartArchivist.Infrastructure.RabbitMq
{
    /// <summary>
    /// Publishes messages to RabbitMQ queues with thread-safe channel access.
    /// </summary>
    public class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IMessageSerializer _messageSerializer;
        private readonly ILogger<RabbitMqPublisher> _logger;
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public RabbitMqPublisher(RabbitMqConfig config, IMessageSerializer messageSerializer, ILogger<RabbitMqPublisher> logger)
        {
            _messageSerializer = messageSerializer;
            _logger = logger;

            // Create connection and channel using factory
            var connectionFactory = new RabbitMqConnectionBuilder(config, logger);
            _connection = connectionFactory.CreateConnection();
            _channel = connectionFactory.CreateChannel(_connection);
        }

        public async Task PublishAsync<TMessage>(TMessage message, string queueName)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            } 
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException("Queue name cannot be empty", nameof(queueName));
            }

            await _lock.WaitAsync();
            try
            {
                // Serialize message to JSON bytes
                var body = _messageSerializer.Serialize(message);

                // Declare queue
                await _channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true, // Queue survives broker restart
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                var properties = new BasicProperties
                {
                    DeliveryMode = DeliveryModes.Persistent
                };

                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties, 
                    body: body
                );

                _logger.LogInformation(
                    "Published message of type {MessageType} to queue {QueueName}",
                    typeof(TMessage).Name,
                    queueName
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to queue {QueueName}", queueName);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            _channel.Dispose();
            _connection.Dispose();
        }
    }
}
