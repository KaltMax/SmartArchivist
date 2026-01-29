using SmartArchivist.Contract.Logger;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Application.Services;
using Microsoft.AspNetCore.SignalR;
using SmartArchivist.Api.Hubs;

namespace SmartArchivist.Api.Workers
{
    /// <summary>
    /// Background service that listens for document update messages and coordinates client notifications when
    /// document processing is completed.
    /// </summary>
    public class DocumentResultWorker : BackgroundService
    {
        private readonly ILoggerWrapper<DocumentResultWorker> _logger;
        private readonly IRabbitMqConsumer _consumer;
        private readonly IHubContext<DocumentHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;

        public DocumentResultWorker(
            ILoggerWrapper<DocumentResultWorker> logger,
            IRabbitMqConsumer consumer,
            IHubContext<DocumentHub> hubContext,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _consumer = consumer;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DocumentUpdate Worker starting...");

            _consumer.Subscribe<IndexingCompletedMessage>(
                QueueNames.DocumentResultQueue,
                async (message) =>
                {
                    _logger.LogInformation("Received document completion for DocumentId: {DocumentId}", message.DocumentId);

                    try
                    {
                        // Mark document as fully completed
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                            await documentService.UpdateDocumentStateAsync(message.DocumentId, DocumentState.Completed);
                        }

                        _logger.LogInformation("Marked document {DocumentId} as Completed", message.DocumentId);

                        // Notify all WebUI clients via SignalR
                        await _hubContext.Clients.All
                            .SendAsync("DocumentProcessingCompleted", new
                            {
                                documentId = message.DocumentId,
                                fileName = message.FileName,
                                status = "Completed"
                            }, stoppingToken);

                        _logger.LogInformation("Notified clients about document {DocumentId} completion", message.DocumentId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing document completion for {DocumentId}", message.DocumentId);
                        throw; // Rethrow to trigger message requeue
                    }
                });

            _logger.LogInformation("DocumentUpdate worker is now listening for messages on queue: {QueueName}", QueueNames.DocumentResultQueue);

            // Keep the worker alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}