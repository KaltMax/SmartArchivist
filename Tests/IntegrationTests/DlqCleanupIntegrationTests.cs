using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SmartArchivist.Contract;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Entities;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.Api.Hubs;
using SmartArchivist.Api.Workers;
using Tests.IntegrationTests.Infrastructure;

namespace Tests.IntegrationTests
{
    [Collection("IntegrationTests")]
    public class DlqCleanupIntegrationTests : IntegrationTestBase, IAsyncLifetime
    {
        private InMemoryMessageBroker? _messageBroker;
        private CancellationTokenSource? _cancellationTokenSource;

        public DlqCleanupIntegrationTests(IntegrationTestFixture fixture) : base(fixture)
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
        /// OCR DLQ Cleanup - Document deleted and user notified after OCR failure
        /// Tests: Real DLQ handler ? Message Broker ? DocumentService ? Repository ? PostgreSQL
        /// Validates: Document deletion, SignalR notification via real handler message flow
        /// </summary>
        [Fact]
        public async Task OcrFailure_DlqCleanup_DeletesDocumentAndNotifiesUser()
        {
            // Arrange - Insert document into database
            var documentId = Guid.NewGuid();
            var fileName = "ocr-failure-test.pdf";

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
                    State = DocumentState.Failed
                };
                await repository.AddAsync(document);
            }

            // Setup SignalR mock to verify notification
            var mockHubContext = Substitute.For<IHubContext<DocumentHub>>();
            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(documentId.ToString()).Returns(mockClientProxy);

            // Create real DLQ handler with InMemoryMessageBroker
            var handler = new FailedDocumentProcessingHandler(
                Substitute.For<ILoggerWrapper<FailedDocumentProcessingHandler>>(),
                _messageBroker!,
                Factory.Services,
                mockHubContext
            );

            // Start handler -> subscribes to DLQ queues
            await handler.StartAsync(_cancellationTokenSource!.Token);
            await Task.Delay(50);

            // Create DLQ message
            var dlqMessage = new DocumentUploadedMessage
            {
                DocumentId = documentId,
                FileName = fileName,
                StoragePath = $"documents/{documentId}/{fileName}",
                ContentType = "application/pdf"
            };

            // Act - Publish message to DLQ queue
            await _messageBroker!.PublishAsync(dlqMessage, $"{QueueNames.OcrQueue}.dlq");

            // Assert - Document deleted from database
            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var deletedDoc = await repository.GetByIdAsync(documentId);
                Assert.Null(deletedDoc);
            }

            // Assert - SignalR notification sent
            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>()
            );

            // Stop handler
            await handler.StopAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// GenAI DLQ Cleanup - Document deleted and user notified after GenAI failure
        /// Tests: Real DLQ handler ? Message Broker ? DocumentService ? Repository ? PostgreSQL
        /// Validates: Document deletion, SignalR notification via real handler message flow
        /// </summary>
        [Fact]
        public async Task GenAiFailure_DlqCleanup_DeletesDocumentAndNotifiesUser()
        {
            // Arrange - Insert document into database
            var documentId = Guid.NewGuid();
            var fileName = "genai-failure-test.pdf";

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
                    State = DocumentState.Failed,
                    OcrText = "Extracted OCR text"
                };
                await repository.AddAsync(document);
            }

            // Setup SignalR mock to verify notification
            var mockHubContext = Substitute.For<IHubContext<DocumentHub>>();
            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(documentId.ToString()).Returns(mockClientProxy);

            // Create real DLQ handler with InMemoryMessageBroker
            var handler = new FailedDocumentProcessingHandler(
                Substitute.For<ILoggerWrapper<FailedDocumentProcessingHandler>>(),
                _messageBroker!,
                Factory.Services,
                mockHubContext
            );

            // Start handler -> subscribes to DLQ queues
            await handler.StartAsync(_cancellationTokenSource!.Token);
            await Task.Delay(50);

            // Create DLQ message
            var dlqMessage = new OcrCompletedMessage
            {
                DocumentId = documentId,
                FileName = fileName,
                ExtractedText = "Extracted OCR text"
            };

            // Act - Publish message to DLQ queue
            await _messageBroker!.PublishAsync(dlqMessage, $"{QueueNames.GenAiQueue}.dlq");

            // Assert - Document deleted from database
            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var deletedDoc = await repository.GetByIdAsync(documentId);
                Assert.Null(deletedDoc);
            }

            // Assert - SignalR notification sent
            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>()
            );

            // Stop handler
            await handler.StopAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// Indexing DLQ Cleanup - Document deleted and user notified after indexing failure
        /// Tests: Real DLQ handler ? Message Broker ? DocumentService ? Repository ? PostgreSQL
        /// Validates: Document deletion, SignalR notification via real handler message flow
        /// </summary>
        [Fact]
        public async Task IndexingFailure_DlqCleanup_DeletesDocumentAndNotifiesUser()
        {
            // Arrange - Insert document into database
            var documentId = Guid.NewGuid();
            var fileName = "indexing-failure-test.pdf";

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
                    State = DocumentState.Failed,
                    OcrText = "Extracted OCR text",
                    GenAiSummary = "Generated summary",
                    Tags = new[] { "tag1", "tag2" }
                };
                await repository.AddAsync(document);
            }

            // Setup SignalR mock to verify notification
            var mockHubContext = Substitute.For<IHubContext<DocumentHub>>();
            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(documentId.ToString()).Returns(mockClientProxy);

            // Create real DLQ handler with InMemoryMessageBroker
            var handler = new FailedDocumentProcessingHandler(
                Substitute.For<ILoggerWrapper<FailedDocumentProcessingHandler>>(),
                _messageBroker!,
                Factory.Services,
                mockHubContext
            );

            // Start handler -> it subscribes to DLQ queues
            await handler.StartAsync(_cancellationTokenSource!.Token);
            await Task.Delay(50);

            // Create DLQ message
            var dlqMessage = new IndexingCompletedMessage
            {
                DocumentId = documentId,
                FileName = fileName
            };

            // Act - Publish message to DLQ queue
            await _messageBroker!.PublishAsync(dlqMessage, $"{QueueNames.IndexingQueue}.dlq");

            // Assert - Document deleted from database
            using (var scope = Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var deletedDoc = await repository.GetByIdAsync(documentId);
                Assert.Null(deletedDoc);
            }

            // Assert - SignalR notification sent
            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>()
            );

            // Stop handler
            await handler.StopAsync(_cancellationTokenSource.Token);
        }
    }
}