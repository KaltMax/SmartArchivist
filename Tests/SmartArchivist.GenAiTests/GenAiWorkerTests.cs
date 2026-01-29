using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.GenAi;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.GenAi.Workers;

namespace Tests.SmartArchivist.GenAiTests
{
    public class GenAiWorkerTests
    {
        private readonly ILoggerWrapper<GenAiWorker> _mockLogger;
        private readonly IRabbitMqConsumer _mockConsumer;
        private readonly IRabbitMqPublisher _mockPublisher;
        private readonly IGenAiSummaryService _mockGenAiService;
        private readonly IDocumentRepository _mockDocumentRepository;
        private readonly GenAiWorker _worker;

        public GenAiWorkerTests()
        {
            _mockLogger = Substitute.For<ILoggerWrapper<GenAiWorker>>();
            _mockConsumer = Substitute.For<IRabbitMqConsumer>();
            _mockPublisher = Substitute.For<IRabbitMqPublisher>();
            _mockGenAiService = Substitute.For<IGenAiSummaryService>();
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

            _worker = new GenAiWorker(
                _mockLogger,
                _mockConsumer,
                _mockPublisher,
                _mockGenAiService,
                mockServiceProvider
            );
        }

        private async Task<Func<OcrCompletedMessage, Task>> GetMessageHandler()
        {
            Func<OcrCompletedMessage, Task>? handler = null;
            _mockConsumer.Subscribe(
                Arg.Any<string>(),
                Arg.Do<Func<OcrCompletedMessage, Task>>(h => handler = h)
            );

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);
            try
            {
                await _worker.StartAsync(cts.Token);
                await Task.Delay(50);
            }
            catch (TaskCanceledException) { }

            return handler!;
        }

        [Fact]
        public async Task HandleOcrCompletedAsync_Success_GeneratesAndPublishesSummary()
        {
            // Arrange
            var message = new OcrCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                ExtractedText = "Document content"
            };
            var genAiResult = new GenAiResult
            {
                Summary = "Generated summary",
                Tags = ["tag1", "tag2"]
            };

            _mockGenAiService.GenerateSummaryAsync(message.ExtractedText).Returns(genAiResult);

            var handler = await GetMessageHandler();

            // Act
            await handler(message);

            // Assert
            await _mockGenAiService.Received(1).GenerateSummaryAsync(message.ExtractedText);
            await _mockDocumentRepository.Received(1).UpdateGenAiResultAsync(message.DocumentId, genAiResult);
            await _mockDocumentRepository.Received(1).UpdateStateAsync(message.DocumentId, DocumentState.GenAiCompleted);
            await _mockPublisher.Received(1).PublishAsync(
                Arg.Is<GenAiCompletedMessage>(m =>
                    m.DocumentId == message.DocumentId &&
                    m.FileName == message.FileName &&
                    m.Summary == genAiResult.Summary &&
                    m.Tags == genAiResult.Tags &&
                    m.ExtractedText == message.ExtractedText
                ),
                QueueNames.IndexingQueue
            );
        }

        [Fact]
        public async Task HandleOcrCompletedAsync_Failure_MarksDocumentAsFailed()
        {
            // Arrange
            var message = new OcrCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                ExtractedText = "text"
            };

            _mockGenAiService.GenerateSummaryAsync(Arg.Any<string>())
                .Throws(new Exception("API error"));

            var handler = await GetMessageHandler();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => handler(message));

            await _mockDocumentRepository.Received(1).UpdateStateAsync(message.DocumentId, DocumentState.Failed);
            _mockLogger.Received().LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to generate summary")),
                Arg.Any<object[]>()
            );
        }

        [Fact]
        public async Task HandleOcrCompletedAsync_StateUpdateFails_LogsAdditionalError()
        {
            // Arrange
            var message = new OcrCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                ExtractedText = "text"
            };

            _mockGenAiService.GenerateSummaryAsync(Arg.Any<string>())
                .Throws(new Exception("API error"));
            _mockDocumentRepository.UpdateStateAsync(Arg.Any<Guid>(), DocumentState.Failed)
                .Throws(new Exception("State update failed"));

            var handler = await GetMessageHandler();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => handler(message));

            _mockLogger.Received(1).LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to generate summary")),
                Arg.Any<object[]>()
            );
            _mockLogger.Received(1).LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Failed to update document") && s.Contains("state to Failed")),
                Arg.Any<object[]>()
            );
        }

        [Fact]
        public async Task StartAsync_SubscribesToGenAiQueue()
        {
            // Arrange & Act
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);
            try
            {
                await _worker.StartAsync(cts.Token);
                await Task.Delay(50);
            }
            catch (TaskCanceledException) { }

            // Assert
            _mockConsumer.Received(1).Subscribe(
                QueueNames.GenAiQueue,
                Arg.Any<Func<OcrCompletedMessage, Task>>()
            );
        }
    }
}