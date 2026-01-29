using SmartArchivist.Contract.Abstractions.Messaging;
using System.Collections.Concurrent;

namespace Tests.IntegrationTests.Infrastructure
{
    /// <summary>
    /// In-memory message broker for integration testing.
    /// Processes messages synchronously to avoid timing issues in tests.
    /// </summary>
    public class InMemoryMessageBroker : IRabbitMqPublisher, IRabbitMqConsumer
    {
        private readonly ConcurrentDictionary<string, List<Delegate>> _handlers = new();
        private readonly ConcurrentQueue<(string QueueName, object Message)> _messageQueue = new();

        // Publishes a message and immediately processes it through registered handlers
        public async Task PublishAsync<TMessage>(TMessage message, string queueName)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            Console.WriteLine($"[MessageBroker] Publishing {typeof(TMessage).Name} to queue: {queueName}");

            _messageQueue.Enqueue((queueName, message));
            await ProcessQueueAsync();
        }

        // Subscribes a handler to a specific queue
        public void Subscribe<TMessage>(string queueName, Func<TMessage, Task> messageHandler)
        {
            var handlers = _handlers.GetOrAdd(queueName, _ => new List<Delegate>());
            lock (handlers)
            {
                handlers.Add(messageHandler);
                Console.WriteLine($"[MessageBroker] Subscribed handler for {typeof(TMessage).Name} on queue: {queueName}");
            }
        }

        // Processes all messages in the queue synchronously
        private async Task ProcessQueueAsync()
        {
            while (_messageQueue.TryDequeue(out var item))
            {
                Console.WriteLine($"[MessageBroker] Processing message on queue: {item.QueueName}");

                if (_handlers.TryGetValue(item.QueueName, out var handlers))
                {
                    foreach (var handler in handlers.ToList())
                    {
                        try
                        {
                            var method = handler.GetType().GetMethod("Invoke");
                            if (method != null)
                            {
                                var task = method.Invoke(handler, new[] { item.Message }) as Task;
                                if (task != null)
                                {
                                    await task;
                                    Console.WriteLine("[MessageBroker] Handler completed successfully");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MessageBroker] Handler failed: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _handlers.Clear();
            _messageQueue.Clear();
        }
    }
}
