using Microsoft.Extensions.Logging;
using SmartArchivist.Contract.Abstractions.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SmartArchivist.Infrastructure.RabbitMq
{
    /// <summary>
    /// Consumes messages from RabbitMQ queues and handles them asynchronously.
    /// </summary>
    public class RabbitMqConsumer : IRabbitMqConsumer
    {
        private readonly ILogger<RabbitMqConsumer> _logger;
        private readonly IMessageSerializer _messageSerializer;
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private const int MaxRetryCount = 2;
        private bool _disposed;

        public RabbitMqConsumer(RabbitMqConfig config, IMessageSerializer messageSerializer, ILogger<RabbitMqConsumer> logger)
        {
            _logger = logger;
            _messageSerializer = messageSerializer;

            // Create connection and channel using factory
            var connectionFactory = new RabbitMqConnectionBuilder(config, logger);
            _connection = connectionFactory.CreateConnection();
            _channel = connectionFactory.CreateChannel(_connection);
        }

        public void Subscribe<TMessage>(string queueName, Func<TMessage, Task> messageHandler)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
               throw new ArgumentException("Queue name cannot be empty", nameof(queueName)); 
            } 
            if (messageHandler == null)
            {
                throw new ArgumentNullException(nameof(messageHandler));
            } 

            Task.Run(async () => await SubscribeAsync(queueName, messageHandler));
        }

        private async Task SubscribeAsync<TMessage>(string queueName, Func<TMessage, Task> messageHandler)
        {
            // Declare queue
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,        // Queue survives broker restart
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Set prefetch count to 1 for fair distribution across multiple consumers
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            // Setup async consumer with event handler
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                await HandleMessageAsync(queueName, messageHandler, eventArgs);
            };

            // Start consuming (autoAck: false means manual acknowledgment)
            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );

            _logger.LogInformation("Started consuming messages from queue {QueueName}", queueName);
        }

        private async Task HandleMessageAsync<TMessage>(string queueName, Func<TMessage, Task> messageHandler, BasicDeliverEventArgs eventArgs)
        {
            try
            {
                // Deserialize message from JSON bytes
                var message = _messageSerializer.Deserialize<TMessage>(eventArgs.Body.ToArray());

                _logger.LogInformation(
                    "Received message of type {MessageType} from queue {QueueName}",
                    typeof(TMessage).Name,
                    queueName
                );

                // Process message with provided handler
                await messageHandler(message);

                // Acknowledge successful processing (removes message from queue)
                await _channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);

                _logger.LogInformation(
                    "Successfully processed message from queue {QueueName}",
                    queueName
                );
            }
            catch (Exception ex)
            {
                // Get retry count from message headers
                int retryCount = 0;
                if (eventArgs.BasicProperties.Headers?.TryGetValue("x-retry-count", out var header) is true)
                {
                    retryCount = Convert.ToInt32(header);
                }

                if (retryCount >= MaxRetryCount)
                {
                    _logger.LogError(ex, "Max retries ({MaxRetries}) exceeded for queue {QueueName}. Moving to DLQ.", MaxRetryCount, queueName);

                    // ACK the message (remove from the queue)
                    await _channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);

                    // Ensure DLQ exists
                    string dlqName = $"{queueName}.dlq";
                    await _channel.QueueDeclareAsync(
                        queue: dlqName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    // Publish to DLQ
                    await _channel.BasicPublishAsync("", dlqName, body: eventArgs.Body);
                }
                else
                {
                    _logger.LogWarning(ex, "Retry attempt {RetryAttempt}/{MaxRetries} for queue {QueueName}", retryCount + 1, MaxRetryCount, queueName);

                    // ACK original message
                    await _channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);

                    // Republish with incremented retry header
                    var properties = new BasicProperties
                    {
                        Headers = new Dictionary<string, object>
                        {
                            ["x-retry-count"] = retryCount + 1
                        }!
                    };

                    await _channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: queueName,
                        mandatory: false,
                        basicProperties: properties,
                        body: eventArgs.Body);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _channel.Dispose();
            _connection.Dispose();
            _disposed = true;

            _logger.LogInformation("RabbitMQ consumer disposed");
        }
    }
}
