using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Application.Services;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Api.Hubs;
using SmartArchivist.Api.Workers;

namespace Tests.SmartArchivist.ApiTests
{
    public class DocumentResultsWorkerTests
    {
        private readonly ILoggerWrapper<DocumentResultWorker> _mockLogger;
        private readonly IRabbitMqConsumer _mockConsumer;
        private readonly IDocumentService _mockDocumentService;
        private readonly IClientProxy _mockClientProxy;
        private readonly DocumentResultWorker _worker;

        public DocumentResultsWorkerTests()
        {
            _mockLogger = Substitute.For<ILoggerWrapper<DocumentResultWorker>>();
            _mockConsumer = Substitute.For<IRabbitMqConsumer>();
            var mockHubContext = Substitute.For<IHubContext<DocumentHub>>();
            _mockDocumentService = Substitute.For<IDocumentService>();
            _mockClientProxy = Substitute.For<IClientProxy>();

            var mockServiceProvider = Substitute.For<IServiceProvider>();
            var mockScope = Substitute.For<IServiceScope>();
            var mockScopedServiceProvider = Substitute.For<IServiceProvider>();
            var mockScopeFactory = Substitute.For<IServiceScopeFactory>();

            // Setup the scope factory to return the mock scope
            mockScopeFactory.CreateScope().Returns(mockScope);
            
            // Setup the service provider to return the scope factory
            mockServiceProvider.GetService(typeof(IServiceScopeFactory)).Returns(mockScopeFactory);
            
            // Setup the scoped service provider to return the document service
            mockScopedServiceProvider.GetService(typeof(IDocumentService)).Returns(_mockDocumentService);
            mockScope.ServiceProvider.Returns(mockScopedServiceProvider);

            var mockClients = Substitute.For<IHubClients>();
            mockHubContext.Clients.Returns(mockClients);
            mockClients.All.Returns(_mockClientProxy);

            _worker = new DocumentResultWorker(
                _mockLogger,
                _mockConsumer,
                mockHubContext,
                mockServiceProvider
            );
        }

        private async Task<Func<IndexingCompletedMessage, Task>> GetMessageHandler()
        {
            Func<IndexingCompletedMessage, Task>? handler = null;
            _mockConsumer.Subscribe(
                Arg.Any<string>(),
                Arg.Do<Func<IndexingCompletedMessage, Task>>(h => handler = h)
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
        public async Task HandleIndexingCompletedAsync_Success_MarksCompletedAndNotifiesClients()
        {
            // Arrange
            var message = new IndexingCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
            };

            var handler = await GetMessageHandler();

            // Act
            await handler(message);

            // Assert
            await _mockDocumentService.Received(1).UpdateDocumentStateAsync(
                message.DocumentId,
                DocumentState.Completed
            );

            await _mockClientProxy.Received(1).SendCoreAsync(
                "DocumentProcessingCompleted",
                Arg.Is<object[]>(args =>
                    args.Length > 0
                ),
                Arg.Any<CancellationToken>()
            );
        }

        [Fact]
        public async Task HandleIndexingCompletedAsync_Failure_ThrowsException()
        {
            // Arrange
            var message = new IndexingCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "test.pdf",
            };

            _mockDocumentService.UpdateDocumentStateAsync(Arg.Any<Guid>(), Arg.Any<DocumentState>())
                .Throws(new Exception("Database error"));

            var handler = await GetMessageHandler();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => handler(message));

            _mockLogger.Received().LogError(
                Arg.Any<Exception>(),
                Arg.Is<string>(s => s.Contains("Error processing document completion")),
                Arg.Any<object[]>()
            );
        }

        [Fact]
        public async Task StartAsync_SubscribesToDocumentResultQueue()
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
                QueueNames.DocumentResultQueue,
                Arg.Any<Func<IndexingCompletedMessage, Task>>()
            );
        }
    }
}