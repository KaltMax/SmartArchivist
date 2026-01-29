namespace SmartArchivist.Contract.Abstractions.Messaging
{
    /// <summary>
    /// Defines a contract for publishing messages to RabbitMQ queues asynchronously.
    /// </summary>
    public interface IRabbitMqPublisher : IDisposable
    {
        Task PublishAsync<TMessage>(TMessage message, string queueName);
    }
}
