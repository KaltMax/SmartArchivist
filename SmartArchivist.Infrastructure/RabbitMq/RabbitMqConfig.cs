using System.ComponentModel.DataAnnotations;

namespace SmartArchivist.Infrastructure.RabbitMq
{
    /// <summary>
    /// Configuration settings for connecting to a RabbitMQ server.
    /// </summary>
    public class RabbitMqConfig
    {
        [Required]
        public string HostName { get; set; } = string.Empty;
        [Required]
        [Range(1, 65535)]
        public int Port { get; set; }
        [Required]
        public string UserName { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        [Required]
        public string VirtualHost { get; set; } = "/";
    }
}