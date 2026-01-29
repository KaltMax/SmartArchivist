namespace SmartArchivist.Contract.Abstractions.Messaging
{
    /// <summary>
    /// Defines methods for serializing and deserializing messages to and from a binary format.
    /// </summary>
    public interface IMessageSerializer
    {
        byte[] Serialize<T>(T message);
        public T Deserialize<T>(byte[] bytes);
    }
}
