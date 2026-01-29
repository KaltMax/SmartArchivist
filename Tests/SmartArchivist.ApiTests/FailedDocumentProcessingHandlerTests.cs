using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Application.Services;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Api.Hubs;
using SmartArchivist.Api.Workers;

namespace Tests.SmartArchivist.ApiTests
{
    public class FailedDocumentProcessingHandlerTests
    {
        private readonly ILoggerWrapper<FailedDocumentProcessingHandler> _mockLogger;
        private readonly IRabbitMqConsumer _mockConsumer;
        private readonly IHubContext<DocumentHub> _mockHubContext;
        private readonly IDocumentService _mockDocumentService;
        private readonly FailedDocumentProcessingHandler _handler;

        public FailedDocumentProcessingHandlerTests()
        {
            _mockLogger = Substitute.For<ILoggerWrapper<FailedDocumentProcessingHandler>>();
            _mockConsumer = Substitute.For<IRabbitMqConsumer>();
            var mockServiceProvider = Substitute.For<IServiceProvider>();
            _mockHubContext = Substitute.For<IHubContext<DocumentHub>>();
            _mockDocumentService = Substitute.For<IDocumentService>();

            var mockScope = Substitute.For<IServiceScope>();
            var mockScopeServiceProvider = Substitute.For<IServiceProvider>();
            mockScopeServiceProvider.GetService(typeof(IDocumentService)).Returns(_mockDocumentService);
            mockScope.ServiceProvider.Returns(mockScopeServiceProvider);

            var mockScopeFactory = Substitute.For<IServiceScopeFactory>();
            mockScopeFactory.CreateScope().Returns(mockScope);
            mockServiceProvider.GetService(typeof(IServiceScopeFactory)).Returns(mockScopeFactory);

            _handler = new FailedDocumentProcessingHandler(_mockLogger, _mockConsumer, mockServiceProvider, _mockHubContext);
        }

        private async Task<Func<DocumentUploadedMessage, Task>> CaptureOcrDlqHandler()
        {
            Func<DocumentUploadedMessage, Task>? handler = null;
            _mockConsumer.Subscribe($"{QueueNames.OcrQueue}.dlq", Arg.Do<Func<DocumentUploadedMessage, Task>>(h => handler = h));

            using var cts = new CancellationTokenSource(100);
            try { await _handler.StartAsync(cts.Token); await Task.Delay(50); }
            catch (TaskCanceledException) { }

            Assert.NotNull(handler);
            return handler;
        }

        private async Task<Func<OcrCompletedMessage, Task>> CaptureGenAiDlqHandler()
        {
            Func<OcrCompletedMessage, Task>? handler = null;
            _mockConsumer.Subscribe($"{QueueNames.GenAiQueue}.dlq", Arg.Do<Func<OcrCompletedMessage, Task>>(h => handler = h));

            using var cts = new CancellationTokenSource(100);
            try { await _handler.StartAsync(cts.Token); await Task.Delay(50); }
            catch (TaskCanceledException) { }

            Assert.NotNull(handler);
            return handler;
        }

        private async Task<Func<GenAiCompletedMessage, Task>> CaptureDocUpdateDlqHandler()
        {
            Func<GenAiCompletedMessage, Task>? handler = null;
            _mockConsumer.Subscribe($"{QueueNames.DocumentResultQueue}.dlq", Arg.Do<Func<GenAiCompletedMessage, Task>>(h => handler = h));

            using var cts = new CancellationTokenSource(100);
            try { await _handler.StartAsync(cts.Token); await Task.Delay(50); }
            catch (TaskCanceledException) { }

            Assert.NotNull(handler);
            return handler;
        }

        private async Task<Func<IndexingCompletedMessage, Task>> CaptureDocIndexingDlqHandler()
        {
            Func<IndexingCompletedMessage, Task>? handler = null;
            _mockConsumer.Subscribe($"{QueueNames.IndexingQueue}.dlq", Arg.Do<Func<IndexingCompletedMessage, Task>>(h => handler = h));
            
            using var cts = new CancellationTokenSource(100);
            try { await _handler.StartAsync(cts.Token); await Task.Delay(50); }
            catch (TaskCanceledException) { }
            
            Assert.NotNull(handler);
            return handler;
        }

        [Fact]
        public async Task HandleOcrFailure_Success()
        {
            // Arrange
            var message = new DocumentUploadedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                StoragePath = "/path/test.pdf",
                ContentType = "application/pdf"
            };

            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            _mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(message.DocumentId.ToString()).Returns(mockClientProxy);

            var handler = await CaptureOcrDlqHandler();

            // Act
            await handler(message);

            // Assert
            await _mockDocumentService.Received(1).DeleteDocumentAsync(message.DocumentId);
            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleGenAiFailure_Success()
        {
            // Arrange
            var message = new OcrCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                ExtractedText = "some text"
            };

            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            _mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(message.DocumentId.ToString()).Returns(mockClientProxy);

            var handler = await CaptureGenAiDlqHandler();

            // Act
            await handler(message);

            // Assert
            await _mockDocumentService.Received(1).DeleteDocumentAsync(message.DocumentId);
            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleDocumentIndexingFailure_Success()
        {
            // Arrange
            var message = new IndexingCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
            };

            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            _mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(message.DocumentId.ToString()).Returns(mockClientProxy);

            var handler = await CaptureDocIndexingDlqHandler();

            // Act
            await handler(message);

            // Assert
            await _mockDocumentService.Received(1).DeleteDocumentAsync(message.DocumentId);
            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleDocumentUpdateFailure_Success()
        {
            // Arrange
            var message = new GenAiCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                ExtractedText = "text",
                Summary = "summary",
                Tags = ["tag1", "tag2"]
            };

            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            _mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(message.DocumentId.ToString()).Returns(mockClientProxy);

            var handler = await CaptureDocUpdateDlqHandler();

            // Act
            await handler(message);

            // Assert
            await _mockDocumentService.Received(1).DeleteDocumentAsync(message.DocumentId);
            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleFailure_RollbackError_SendsFailureNotification()
        {
            // Arrange
            var message = new DocumentUploadedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
                StoragePath = "/path/test.pdf",
                ContentType = "application/pdf"
            };

            _mockDocumentService.DeleteDocumentAsync(Arg.Any<Guid>())
                .Throws(new Exception("Delete failed"));

            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            _mockHubContext.Clients.Returns(mockClients);
            mockClients.Group(message.DocumentId.ToString()).Returns(mockClientProxy);

            var handler = await CaptureOcrDlqHandler();

            // Act
            await handler(message);

            // Assert
            _mockLogger.Received().LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Error handling rollback")),
                Arg.Any<object[]>());

            await mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingFailed",
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ExecuteAsync_SubscribesToAllDlqQueues()
        {
            // Arrange
            using var cts = new CancellationTokenSource(100);

            // Act
            try { await _handler.StartAsync(cts.Token); await Task.Delay(50); }
            catch (TaskCanceledException) { }

            // Assert
            _mockConsumer.Received(1).Subscribe($"{QueueNames.OcrQueue}.dlq", Arg.Any<Func<DocumentUploadedMessage, Task>>());
            _mockConsumer.Received(1).Subscribe($"{QueueNames.GenAiQueue}.dlq", Arg.Any<Func<OcrCompletedMessage, Task>>());
            _mockConsumer.Received(1).Subscribe($"{QueueNames.DocumentResultQueue}.dlq", Arg.Any<Func<GenAiCompletedMessage, Task>>());
        }
    }
}
