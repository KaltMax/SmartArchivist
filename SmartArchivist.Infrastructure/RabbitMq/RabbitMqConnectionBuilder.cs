using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace SmartArchivist.Infrastructure.RabbitMq
{
    /// <summary>
    /// Builds and manages RabbitMQ connections and channels.
    /// </summary>
    public class RabbitMqConnectionBuilder
    {
        private readonly RabbitMqConfig _config;
        private readonly ILogger _logger;

        public RabbitMqConnectionBuilder(RabbitMqConfig config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public IConnection CreateConnection()
        {
            // Create connection factory with automatic recovery enabled
            // This ensures the connection automatically reconnects if it drops
            var factory = new ConnectionFactory
            {
                HostName = _config.HostName,
                Port = _config.Port,
                UserName = _config.UserName,
                Password = _config.Password,
                VirtualHost = _config.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            // Establish connection
            _logger.LogInformation("Establishing connection to RabbitMQ at {HostName}:{Port}", _config.HostName, _config.Port);
            var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _logger.LogInformation("Successfully connected to RabbitMQ");

            return connection;
        }

        public IChannel CreateChannel(IConnection connection)
        {
            return connection.CreateChannelAsync().GetAwaiter().GetResult();
        }
    }
}
