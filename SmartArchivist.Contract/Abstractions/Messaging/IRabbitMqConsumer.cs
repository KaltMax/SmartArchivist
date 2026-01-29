namespace SmartArchivist.Contract.Abstractions.Messaging
{
    /// <summary>
    /// Defines a contract for consuming messages from a RabbitMQ queue and processing them asynchronously.
    /// </summary>
    public interface IRabbitMqConsumer : IDisposable
    {
        void Subscribe<TMessage>(string queueName, Func<TMessage, Task> messageHandler);
    }
}
