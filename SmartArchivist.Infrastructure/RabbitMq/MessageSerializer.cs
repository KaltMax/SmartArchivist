using System.Text;
using System.Text.Json;
using SmartArchivist.Contract.Abstractions.Messaging;

namespace SmartArchivist.Infrastructure.RabbitMq
{
    /// <summary>
    /// Serializes and deserializes messages to/from JSON for RabbitMQ transport.
    /// </summary>
    public class MessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Serializes a message object to UTF-8 JSON bytes.
        public byte[] Serialize<T>(T message)
        {
            var json = JsonSerializer.Serialize(message, Options);
            return Encoding.UTF8.GetBytes(json);
        }

        // Deserializes UTF-8 JSON bytes to a message object.
        public T Deserialize<T>(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, Options)
                   ?? throw new InvalidOperationException($"Failed to deserialize message of type {typeof(T).Name}");
        }
    }
}
