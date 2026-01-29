using System.ComponentModel.DataAnnotations;

namespace SmartArchivist.Infrastructure.MinIo
{
    /// <summary>
    /// Configuration for MinIO connection (analog to RabbitMqConfig).
    /// </summary>
    public class MinioConfig
    {
        [Required]
        public string Endpoint { get; set; } = string.Empty;
        [Required]
        public string AccessKey { get; set; } = string.Empty;
        [Required]
        public string SecretKey { get; set; } = string.Empty;
        [Required]
        public string BucketName { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = false;
    }
}