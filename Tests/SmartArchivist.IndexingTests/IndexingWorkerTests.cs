using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.Indexing.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.SmartArchivist.IndexingTests
{
    public class IndexingWorkerTests
    {
        private readonly ILoggerWrapper<IndexingWorker> _logger;
        private readonly IRabbitMqConsumer _consumer;
        private readonly IRabbitMqPublisher _publisher;
        private readonly IIndexingService _indexingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceScope _scope;
        private readonly IDocumentRepository _documentRepository;
        private readonly IndexingWorker _sut;

        public IndexingWorkerTests()
        {
            _logger = Substitute.For<ILoggerWrapper<IndexingWorker>>();
            _consumer = Substitute.For<IRabbitMqConsumer>();
            _publisher = Substitute.For<IRabbitMqPublisher>();
            _indexingService = Substitute.For<IIndexingService>();
            _serviceProvider = Substitute.For<IServiceProvider>();
            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _scope = Substitute.For<IServiceScope>();
            _documentRepository = Substitute.For<IDocumentRepository>();

            // Setup ServiceProvider mock chain
            _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(_scopeFactory);
            _scopeFactory.CreateScope().Returns(_scope);
            _scope.ServiceProvider.Returns(_serviceProvider);
            _serviceProvider.GetService(typeof(IDocumentRepository)).Returns(_documentRepository);

            _sut = new IndexingWorker(
                _logger,
                _consumer,
                _publisher,
                _indexingService,
                _serviceProvider
            );
        }

        #region Success Path Tests

        [Fact]
        public async Task HandleGenAiCompleted_Success_ExecutesCompleteWorkflow()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var fileName = "test.pdf";
            var message = new GenAiCompletedMessage
            {
                DocumentId = documentId,
                FileName = fileName,
                ExtractedText = "Sample text",
                Summary = "Test summary",
                Tags = new[] { "tag1", "tag2" }
            };

            Func<GenAiCompletedMessage, Task>? capturedHandler = null;
            _consumer.Subscribe(
                Arg.Any<string>(),
                Arg.Do<Func<GenAiCompletedMessage, Task>>(handler => capturedHandler = handler)
            );

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));
            await _sut.StartAsync(cts.Token);
            await Task.Delay(50);

            // Act
            if (capturedHandler != null)
            {
                await capturedHandler(message);
            }

            // Assert
            await _indexingService.Received(1).IndexDocumentAsync(
                documentId,
                fileName,
                message.ExtractedText,
                message.Summary,
                message.Tags
            );

            await _documentRepository.Received(1).UpdateStateAsync(documentId, DocumentState.Indexed);

            await _publisher.Received(1).PublishAsync(
                Arg.Is<IndexingCompletedMessage>(m =>
                    m.DocumentId == documentId &&
                    m.FileName == fileName
                ),
                QueueNames.DocumentResultQueue
            );

            _scope.Received(1).Dispose();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task HandleGenAiCompleted_IndexingFails_MarksDocumentAsFailedAndRethrows()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var message = new GenAiCompletedMessage
            {
                DocumentId = documentId,
                FileName = "test.pdf",
                ExtractedText = "Text",
                Summary = "Summary",
                Tags = new[] { "tag1" }
            };

            var expectedException = new Exception("ElasticSearch error");
            _indexingService.IndexDocumentAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>()
            ).Throws(expectedException);

            Func<GenAiCompletedMessage, Task>? capturedHandler = null;
            _consumer.Subscribe(
                Arg.Any<string>(),
                Arg.Do<Func<GenAiCompletedMessage, Task>>(handler => capturedHandler = handler)
            );

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));
            await _sut.StartAsync(cts.Token);
            await Task.Delay(50);

            // Act & Assert
            if (capturedHandler != null)
            {
                var thrownException = await Assert.ThrowsAsync<Exception>(() => capturedHandler(message));
                Assert.Equal(expectedException, thrownException);
            }

            await _documentRepository.Received(1).UpdateStateAsync(documentId, DocumentState.Failed);

            await _publisher.DidNotReceive().PublishAsync(
                Arg.Any<IndexingCompletedMessage>(),
                Arg.Any<string>()
            );

            _logger.Received(1).LogError(expectedException, "Error indexing document {DocumentId}", documentId);
        }

        [Fact]
        public async Task HandleGenAiCompleted_StateUpdateFails_LogsErrorAndRethrows()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var message = new GenAiCompletedMessage
            {
                DocumentId = documentId,
                FileName = "test.pdf",
                ExtractedText = "Text",
                Summary = "Summary",
                Tags = new[] { "tag1" }
            };

            var stateException = new Exception("Database error");
            _documentRepository.UpdateStateAsync(documentId, DocumentState.Indexed)
                .Throws(new Exception("First error"));
            _documentRepository.UpdateStateAsync(documentId, DocumentState.Failed)
                .Throws(stateException);

            Func<GenAiCompletedMessage, Task>? capturedHandler = null;
            _consumer.Subscribe(
                Arg.Any<string>(),
                Arg.Do<Func<GenAiCompletedMessage, Task>>(handler => capturedHandler = handler)
            );

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));
            await _sut.StartAsync(cts.Token);
            await Task.Delay(50);

            // Act & Assert
            if (capturedHandler != null)
            {
                await Assert.ThrowsAsync<Exception>(() => capturedHandler(message));
            }

            _logger.Received(1).LogError(
                stateException,
                "Failed to update document {DocumentId} state to Failed",
                documentId
            );
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task HandleGenAiCompleted_WithEmptyTags_IndexesSuccessfully()
        {
            // Arrange
            var message = new GenAiCompletedMessage
            {
                DocumentId = Guid.NewGuid(),
                FileName = "no-tags.pdf",
                ExtractedText = "Text without tags",
                Summary = "Summary without tags",
                Tags = Array.Empty<string>()
            };

            Func<GenAiCompletedMessage, Task>? capturedHandler = null;
            _consumer.Subscribe(
                Arg.Any<string>(),
                Arg.Do<Func<GenAiCompletedMessage, Task>>(handler => capturedHandler = handler)
            );

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));
            await _sut.StartAsync(cts.Token);
            await Task.Delay(50);

            // Act
            if (capturedHandler != null)
            {
                await capturedHandler(message);
            }

            // Assert
            await _indexingService.Received(1).IndexDocumentAsync(
                message.DocumentId,
                message.FileName,
                message.ExtractedText,
                message.Summary,
                Arg.Is<string[]>(tags => tags.Length == 0)
            );
        }

        #endregion
    }
}