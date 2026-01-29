using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Ocr;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.Ocr.Workers;

namespace Tests.SmartArchivist.OcrTests
{
    public class OcrWorkerTests
    {
        private readonly ILoggerWrapper<OcrWorker> _mockLogger;
        private readonly IRabbitMqConsumer _mockConsumer;
        private readonly IRabbitMqPublisher _mockPublisher;
        private readonly IPdfToImageConverter _mockPdfConverter;
        private readonly IFileStorageService _mockFileStorage;
        private readonly IOcrService _mockOcrService;
        private readonly IDocumentRepository _mockDocumentRepository;
        private readonly OcrWorker _worker;

        public OcrWorkerTests()
        {
            _mockLogger = Substitute.For<ILoggerWrapper<OcrWorker>>();
            _mockConsumer = Substitute.For<IRabbitMqConsumer>();
            _mockPublisher = Substitute.For<IRabbitMqPublisher>();
            _mockPdfConverter = Substitute.For<IPdfToImageConverter>();
            _mockFileStorage = Substitute.For<IFileStorageService>();
            _mockOcrService = Substitute.For<IOcrService>();
            _mockDocumentRepository = Substitute.For<IDocumentRepository>();

            var mockServiceProvider = Substitute.For<IServiceProvider>();
            var mockScope = Substitute.For<IServiceScope>();
            var mockScopedServiceProvider = Substitute.For<IServiceProvider>();
            var mockScopeFactory = Substitute.For<IServiceScopeFactory>();

            // Set up the scope factory to return the mock scope
            mockScopeFactory.CreateScope().Returns(mockScope);

            // Set up the service provider to return the scope factory
            mockServiceProvider.GetService(typeof(IServiceScopeFactory)).Returns(mockScopeFactory);

            // Set up the scoped service provider to return the document repository
            mockScopedServiceProvider.GetService(typeof(IDocumentRepository)).Returns(_mockDocumentRepository);
            mockScope.ServiceProvider.Returns(mockScopedServiceProvider);

            _worker = new OcrWorker(
                _mockLogger,
                _mockConsumer,
                _mockPublisher,
                _mockFileStorage,
                _mockPdfConverter,
                _mockOcrService,
                mockServiceProvider
            );
        }

        private async Task<Func<DocumentUploadedMessage, Task>> GetMessageHandler()
        {
            Func<DocumentUploadedMessage, Task>? handler = null;
            _mockConsumer.Subscribe(
                Arg.Any<string>(),
                Arg.Do<Func<DocumentUploadedMessage, Task>>(h => handler = h)
            );

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);
            try
            {
                await _worker.StartAsync(cts.Token);
                await Task.Delay(50, cts.Token);
            }
            catch (TaskCanceledException) { }

            return handler!;
        }

        [Fact]
        public async Task HandleDocumentUploadedAsync_Success_ProcessesAndPublishesMessage()
        {
            // Arrange
            var message = new DocumentUploadedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                StoragePath = "documents/test.pdf",
                ContentType = "application/pdf"
            };

            var mockImages = new List<byte[]> { new byte[] { 0x01, 0x02 } };
            var extractedText = "Extracted text";

            _mockFileStorage.DownloadFileAsync(message.StoragePath)
                .Returns(Task.FromResult<Stream>(new MemoryStream()));
            _mockPdfConverter.ConvertToImagesAsync(Arg.Any<Stream>())
                .Returns(Task.FromResult<IEnumerable<byte[]>>(mockImages));
            _mockOcrService.ExtractTextFromImagesAsync(Arg.Any<IEnumerable<byte[]>>())
                .Returns(Task.FromResult(extractedText));

            var handler = await GetMessageHandler();

            // Act
            await handler(message);

            // Assert
            await _mockDocumentRepository.Received(1).UpdateOcrTextAsync(message.DocumentId, extractedText);
            await _mockDocumentRepository.Received(1).UpdateStateAsync(message.DocumentId, DocumentState.OcrCompleted);
            await _mockPublisher.Received(1).PublishAsync(
                Arg.Is<OcrCompletedMessage>(msg =>
                    msg.DocumentId == message.DocumentId &&
                    msg.FileName == message.FileName &&
                    msg.ExtractedText == extractedText
                ),
                QueueNames.GenAiQueue
            );
        }

        [Fact]
        public async Task HandleDocumentUploadedAsync_Failure_MarksDocumentAsFailed()
        {
            // Arrange
            var message = new DocumentUploadedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                StoragePath = "documents/test.pdf",
                ContentType = "application/pdf"
            };

            _mockFileStorage.DownloadFileAsync(message.StoragePath)
                .Throws(new Exception("Processing failed"));

            var handler = await GetMessageHandler();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => handler(message));

            await _mockDocumentRepository.Received(1).UpdateStateAsync(message.DocumentId, DocumentState.Failed);
            _mockLogger.Received().LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to process OCR")),
                Arg.Any<object[]>()
            );
        }

        [Fact]
        public async Task HandleDocumentUploadedAsync_StateUpdateFails_LogsAdditionalError()
        {
            // Arrange
            var message = new DocumentUploadedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                StoragePath = "documents/test.pdf",
                ContentType = "application/pdf"
            };

            _mockFileStorage.DownloadFileAsync(message.StoragePath)
                .Throws(new Exception("Processing failed"));
            _mockDocumentRepository.UpdateStateAsync(Arg.Any<Guid>(), DocumentState.Failed)
                .Throws(new Exception("State update failed"));

            var handler = await GetMessageHandler();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => handler(message));

            _mockLogger.Received(1).LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to process OCR")),
                Arg.Any<object[]>()
            );
            _mockLogger.Received(1).LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to update document") && s.Contains("state to Failed")),
                Arg.Any<object[]>()
            );
        }

        [Fact]
        public async Task StartAsync_SubscribesToOcrQueue()
        {
            // Arrange & Act
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);
            try
            {
                await _worker.StartAsync(cts.Token);
                await Task.Delay(50, cts.Token);
            }
            catch (TaskCanceledException) { }

            // Assert
            _mockConsumer.Received(1).Subscribe(
                QueueNames.OcrQueue,
                Arg.Any<Func<DocumentUploadedMessage, Task>>()
            );
        }
    }
}