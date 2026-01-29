using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using SmartArchivist.Contract.Abstractions.Storage;

namespace SmartArchivist.Infrastructure.MinIo
{
    /// <summary>
    /// Provides file storage operations using a MinIO object storage backend. Supports uploading, downloading,
    /// deleting, and checking the existence of files in a configured MinIO bucket.
    /// </summary>
    public class MinioFileStorageService : IFileStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly MinioConfig _config;
        private readonly ILogger<MinioFileStorageService> _logger;
        private readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();
        private readonly string[] _dangerousPatterns = ["..", "/", "\\"];
        private const int MaxFileNameLength = 255;

        public MinioFileStorageService(
            MinioConfig config,
            ILogger<MinioFileStorageService> logger)
        {
            _config = config;
            _logger = logger;

            _minioClient = new MinioClient()
                .WithEndpoint(config.Endpoint)
                .WithCredentials(config.AccessKey, config.SecretKey)
                .WithSSL(config.UseSsl)
                .Build();

            _logger.LogInformation(
                "MinIO client initialized: Endpoint={Endpoint}, Bucket={Bucket}, SSL={UseSSL}",
                config.Endpoint,
                config.BucketName,
                config.UseSsl
            );
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing MinIO storage...");

            var bucketExistsArgs = new BucketExistsArgs().WithBucket(_config.BucketName);

            bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs);

            if (!found)
            {
                _logger.LogInformation("Bucket {BucketName} does not exist. Creating...", _config.BucketName);

                var makeBucketArgs = new MakeBucketArgs().WithBucket(_config.BucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs);

                _logger.LogInformation("Successfully created bucket {BucketName}", _config.BucketName);
            }
            else
            {
                _logger.LogInformation("Bucket {BucketName} already exists", _config.BucketName);
            }

            _logger.LogInformation("MinIO storage initialized successfully");
        }

        public async Task<string> UploadFileAsync(Guid documentId, string fileName, byte[] fileContent, string contentType)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty", nameof(fileName));
            }
            if (fileContent == null || fileContent.Length == 0)
            {
                throw new ArgumentException("File content cannot be empty", nameof(fileContent));
            }

            // Sanitize filename and create object path (MinIO-specific concern)
            var objectName = SanitizeFileName(documentId, fileName);

            _logger.LogInformation(
                "Uploading file to MinIO: DocumentId={DocumentId}, FileName={FileName}, ObjectName={ObjectName}, Size={Size} bytes",
                documentId,
                fileName,
                objectName,
                fileContent.Length
            );

            using var stream = new MemoryStream(fileContent);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(fileContent.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs);

            _logger.LogInformation("Successfully uploaded file to MinIO: {ObjectName}", objectName);
            return objectName;
        }

        public async Task<Stream> DownloadFileAsync(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                throw new ArgumentException("Stored path cannot be empty", nameof(storedPath));
            }

            _logger.LogInformation("Downloading file from MinIO: {StoredPath}", storedPath);

            var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(storedPath)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getObjectArgs);

            memoryStream.Position = 0;

            _logger.LogInformation("Successfully downloaded file from MinIO: {StoredPath}, Size={Size} bytes",
                storedPath, memoryStream.Length);

            return memoryStream;
        }

        public async Task DeleteFileAsync(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                throw new ArgumentException("Stored path cannot be empty", nameof(storedPath));
            }

            _logger.LogInformation("Deleting file from MinIO: {StoredPath}", storedPath);

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(storedPath);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);

            _logger.LogInformation("Successfully deleted file from MinIO: {StoredPath}", storedPath);
        }

        private string SanitizeFileName(Guid documentId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty", nameof(fileName));
            }

            // Check for path traversal attempts
            if (_dangerousPatterns.Any(fileName.Contains))
            {
                throw new ArgumentException($"File name contains dangerous pattern: {fileName}", nameof(fileName));
            }

            // Remove invalid characters
            string sanitized = string.Join("_", fileName.Split(_invalidFileNameChars));

            // Remove leading/trailing whitespace and dots
            sanitized = sanitized.Trim().Trim('.');

            // Ensure length is within limits
            if (sanitized.Length > MaxFileNameLength)
            {
                var extension = Path.GetExtension(sanitized);
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
                var maxNameLength = MaxFileNameLength - extension.Length;
                sanitized = nameWithoutExtension[..maxNameLength] + extension;
            }

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                throw new ArgumentException("File name is invalid after sanitization", nameof(fileName));
            }

            return $"{documentId}/{sanitized}";
        }
    }
}