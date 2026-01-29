using Microsoft.Extensions.Logging;
using NSubstitute;
using SmartArchivist.Infrastructure.MinIo;

namespace Tests.SmartArchivist.InfrastructureTests
{
    /// <summary>
    /// Unit tests for MinioFileStorageService focusing on input validation.
    /// Note: Tests that require actual MinIO SDK operations are skipped or marked as integration tests
    /// because MinIO SDK is difficult to mock without a real instance.
    /// </summary>
    public class MinioFileStorageServiceTests
    {
        private readonly ILogger<MinioFileStorageService> _logger;
        private readonly MinioConfig _config;

        public MinioFileStorageServiceTests()
        {
            _logger = Substitute.For<ILogger<MinioFileStorageService>>();
            _config = new MinioConfig
            {
                Endpoint = "localhost:9000",
                AccessKey = "testkey",
                SecretKey = "testsecret",
                BucketName = "test-bucket",
                UseSsl = false
            };
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesWithConfig()
        {
            // Arrange & Act
            var service = new MinioFileStorageService(_config, _logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_LogsInitialization()
        {
            // Arrange & Act
            var service = new MinioFileStorageService(_config, _logger);

            // Assert
            Assert.NotNull(service);
            _logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("MinIO client initialized")),
                null,
                Arg.Any<Func<object, Exception?, string>>()
            );
        }

        [Fact]
        public void Constructor_WithValidConfig_InitializesSuccessfully()
        {
            // Arrange
            var validConfig = new MinioConfig
            {
                Endpoint = "minio:9000",
                AccessKey = "admin",
                SecretKey = "password",
                BucketName = "documents",
                UseSsl = false
            };

            // Act
            var service = new MinioFileStorageService(validConfig, _logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithSslEnabled_InitializesSuccessfully()
        {
            // Arrange
            var sslConfig = new MinioConfig
            {
                Endpoint = "minio:9000",
                AccessKey = "admin",
                SecretKey = "password",
                BucketName = "documents",
                UseSsl = true
            };

            // Act
            var service = new MinioFileStorageService(sslConfig, _logger);

            // Assert
            Assert.NotNull(service);
        }

        #endregion

        #region Input Validation Tests (No Real MinIO Connection Required)

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task UploadFile_EmptyFileName_ThrowsArgumentException(string? invalidName)
        {
            // Arrange
            var sut = new MinioFileStorageService(_config, _logger);
            var documentId = Guid.NewGuid();
            var fileContent = new byte[] { 1, 2, 3 };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.UploadFileAsync(documentId, invalidName!, fileContent, "application/pdf"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task DownloadFile_EmptyStoredPath_ThrowsArgumentException(string? invalidPath)
        {
            // Arrange
            var sut = new MinioFileStorageService(_config, _logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.DownloadFileAsync(invalidPath!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task DeleteFile_EmptyStoredPath_ThrowsArgumentException(string? invalidPath)
        {
            // Arrange
            var sut = new MinioFileStorageService(_config, _logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.DeleteFileAsync(invalidPath!));
        }

        [Fact]
        public async Task UploadFile_EmptyFileContent_ThrowsArgumentException()
        {
            // Arrange
            var sut = new MinioFileStorageService(_config, _logger);
            var documentId = Guid.NewGuid();
            var fileName = "test.pdf";
            var fileContent = Array.Empty<byte>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.UploadFileAsync(documentId, fileName, fileContent, "application/pdf"));
        }

        [Fact]
        public async Task UploadFile_NullFileContent_ThrowsArgumentException()
        {
            // Arrange
            var sut = new MinioFileStorageService(_config, _logger);
            var documentId = Guid.NewGuid();
            var fileName = "test.pdf";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.UploadFileAsync(documentId, fileName, null!, "application/pdf"));
        }

        [Theory]
        [InlineData("../../etc/passwd")]
        [InlineData("../config")]
        [InlineData("folder/file.pdf")]
        [InlineData("folder\\file.pdf")]
        public async Task UploadFile_DangerousFileName_ThrowsArgumentException(string dangerousName)
        {
            // Arrange
            var sut = new MinioFileStorageService(_config, _logger);
            var documentId = Guid.NewGuid();
            var fileContent = new byte[] { 1, 2, 3 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.UploadFileAsync(documentId, dangerousName, fileContent, "application/pdf"));
            Assert.Contains("dangerous pattern", ex.Message);
        }

        #endregion
    }
}