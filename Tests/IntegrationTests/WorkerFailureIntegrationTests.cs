using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.GenAi;
using SmartArchivist.Contract.Abstractions.Ocr;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Entities;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.GenAi.Workers;
using SmartArchivist.Indexing.Workers;
using SmartArchivist.Ocr.Workers;
using Tests.IntegrationTests.Infrastructure;

namespace Tests.IntegrationTests
{
    [Collection("IntegrationTests")]
    public class WorkerFailureIntegrationTests : IntegrationTestBase, IAsyncLifetime
    {
        private InMemoryMessageBroker? _messageBroker;
        private CancellationTokenSource? _cancellationTokenSource;

        public WorkerFailureIntegrationTests(IntegrationTestFixture fixture) : base(fixture)
        {
        }

        public Task InitializeAsync()
        {
            _messageBroker = new InMemoryMessageBroker();
            _cancellationTokenSource = new CancellationTokenSource();
            return Task.CompletedTask;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _messageBroker?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// OcrWorker - Marks document as Failed when OCR extraction fails
        /// Tests: Worker ? Message Broker ? Repository ? PostgreSQL integration
        /// Validates: Exception handling, state transition to Failed, real message flow
        /// </summary>
        [Fact]
        public async Task OcrWorker_WhenExtractionFails_MarksDocumentAsFailed()
        {
            // Arrange - Insert document into database
            var documentId = Guid.NewGuid();
            var fileName = "ocr-worker-failure-test.pdf";

            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var document = new DocumentEntity
                {
                    Id = documentId,
                    Name = fileName.Replace(".pdf", ""),
                    FilePath = $"documents/{documentId}/{fileName}",
                    FileExtension = ".pdf",
                    ContentType = "application/pdf",
                    UploadDate = DateTime.UtcNow,
                    FileSize = 1024,
                    State = DocumentState.Uploaded
                };
                await repository.AddAsync(document);
            }

            // Setup mocks for worker dependencies
            var mockLogger = Substitute.For<ILoggerWrapper<OcrWorker>>();
            var mockFileStorage = Substitute.For<IFileStorageService>();
            var mockPdfConverter = Substitute.For<IPdfToImageConverter>();
            var mockOcrService = Substitute.For<IOcrService>();

            // Mock OCR service to throw exception
            mockFileStorage.DownloadFileAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3 })));
            mockPdfConverter.ConvertToImagesAsync(Arg.Any<Stream>())
                .Returns(Task.FromResult<IEnumerable<byte[]>>(new List<byte[]> { new byte[] { 1, 2, 3 } }));
            mockOcrService.ExtractTextFromImagesAsync(Arg.Any<IEnumerable<byte[]>>())
                .Throws(new Exception("OCR extraction failed"));

            // Create worker with InMemoryMessageBroker
            var worker = new OcrWorker(
                mockLogger,
                _messageBroker!,
                _messageBroker!,
                mockFileStorage,
                mockPdfConverter,
                mockOcrService,
                Factory.Services
            );

            // Start worker -> subscribes to the message broker
            await worker.StartAsync(_cancellationTokenSource!.Token);
            await Task.Delay(50);

            // Create message
            var message = new DocumentUploadedMessage
            {
                DocumentId = documentId,
                FileName = fileName,
                StoragePath = $"documents/{documentId}/{fileName}",
                ContentType = "application/pdf"
            };

            // Act - Publish message to broker
            await Assert.ThrowsAsync<Exception>(async () =>
                await _messageBroker!.PublishAsync(message, QueueNames.OcrQueue)
            );

            // Assert - Document state updated to Failed in database
            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var doc = await repository.GetByIdAsync(documentId);
                Assert.NotNull(doc);
                Assert.Equal(DocumentState.Failed, doc.State);
            }

            // Assert - Error logged
            mockLogger.Received().LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to process OCR")),
                Arg.Any<object[]>()
            );

            await worker.StopAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// GenAiWorker - Marks document as Failed when summary generation fails
        /// Tests: Worker ? Message Broker ? Repository ? PostgreSQL integration
        /// Validates: Exception handling, state transition to Failed, real message flow
        /// </summary>
        [Fact]
        public async Task GenAiWorker_WhenSummaryGenerationFails_MarksDocumentAsFailed()
        {
            // Arrange - Insert document into database
            var documentId = Guid.NewGuid();
            var fileName = "genai-worker-failure-test.pdf";

            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var document = new DocumentEntity
                {
                    Id = documentId,
                    Name = fileName.Replace(".pdf", ""),
                    FilePath = $"documents/{documentId}/{fileName}",
                    FileExtension = ".pdf",
                    ContentType = "application/pdf",
                    UploadDate = DateTime.UtcNow,
                    FileSize = 1024,
                    State = DocumentState.OcrCompleted,
                    OcrText = "Extracted OCR text"
                };
                await repository.AddAsync(document);
            }

            // Setup mocks for worker dependencies
            var mockLogger = Substitute.For<ILoggerWrapper<GenAiWorker>>();
            var mockGenAiService = Substitute.For<IGenAiSummaryService>();

            // Mock GenAI service to throw exception
            mockGenAiService.GenerateSummaryAsync(Arg.Any<string>())
                .Throws(new Exception("GenAI API error"));

            // Create worker with InMemoryMessageBroker
            var worker = new GenAiWorker(
                mockLogger,
                _messageBroker!,
                _messageBroker!,
                mockGenAiService,
                Factory.Services
            );

            // Start worker -> subscribes to the message broker
            await worker.StartAsync(_cancellationTokenSource!.Token);
            await Task.Delay(50);

            // Create message
            var message = new OcrCompletedMessage
            {
                DocumentId = documentId,
                FileName = fileName,
                ExtractedText = "Extracted OCR text"
            };

            // Act - Publish message to broker
            await Assert.ThrowsAsync<Exception>(async () =>
                await _messageBroker!.PublishAsync(message, QueueNames.GenAiQueue)
            );

            // Assert - Document state updated to Failed in database
            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var doc = await repository.GetByIdAsync(documentId);
                Assert.NotNull(doc);
                Assert.Equal(DocumentState.Failed, doc.State);
            }

            // Assert - Error logged
            mockLogger.Received().LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to generate summary")),
                Arg.Any<object[]>()
            );

            // Stop worker
            await worker.StopAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// IndexingWorker - Marks document as Failed when indexing fails
        /// Tests: Worker ? Message Broker ? Repository ? PostgreSQL integration
        /// Validates: Exception handling, state transition to Failed, real message flow
        /// </summary>
        [Fact]
        public async Task IndexingWorker_WhenIndexingFails_MarksDocumentAsFailed()
        {
            // Arrange - Insert document into database
            var documentId = Guid.NewGuid();
            var fileName = "indexing-worker-failure-test.pdf";

            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var document = new DocumentEntity
                {
                    Id = documentId,
                    Name = fileName.Replace(".pdf", ""),
                    FilePath = $"documents/{documentId}/{fileName}",
                    FileExtension = ".pdf",
                    ContentType = "application/pdf",
                    UploadDate = DateTime.UtcNow,
                    FileSize = 1024,
                    State = DocumentState.GenAiCompleted,
                    OcrText = "Extracted OCR text",
                    GenAiSummary = "Generated summary",
                    Tags = new[] { "tag1", "tag2" }
                };
                await repository.AddAsync(document);
            }

            // Setup mocks for worker dependencies
            var mockLogger = Substitute.For<ILoggerWrapper<IndexingWorker>>();
            var mockIndexingService = Substitute.For<IIndexingService>();

            // Mock indexing service to throw exception
            mockIndexingService.IndexDocumentAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>()
            ).Throws(new Exception("Elasticsearch error"));

            // Create worker with InMemoryMessageBroker
            var worker = new IndexingWorker(
                mockLogger,
                _messageBroker!,
                _messageBroker!,
                mockIndexingService,
                Factory.Services
            );

            // Start worker -> subscribes to the message broker
            await worker.StartAsync(_cancellationTokenSource!.Token);

            // Give worker time to subscribe
            await Task.Delay(50);

            // Create message
            var message = new GenAiCompletedMessage
            {
                DocumentId = documentId,
                FileName = fileName,
                ExtractedText = "Extracted OCR text",
                Summary = "Generated summary",
                Tags = new[] { "tag1", "tag2" }
            };

            // Act - Publish message to broker
            await Assert.ThrowsAsync<Exception>(async () =>
                await _messageBroker!.PublishAsync(message, QueueNames.IndexingQueue)
            );

            // Assert - Document state updated to Failed in database
            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var doc = await repository.GetByIdAsync(documentId);
                Assert.NotNull(doc);
                Assert.Equal(DocumentState.Failed, doc.State);
            }

            // Assert - Error logged
            mockLogger.Received().LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Error indexing document")),
                Arg.Any<object[]>()
            );

            // Stop worker
            await worker.StopAsync(_cancellationTokenSource.Token);
        }
    }
}