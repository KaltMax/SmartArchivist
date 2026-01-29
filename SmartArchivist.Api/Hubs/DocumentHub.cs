using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SmartArchivist.Api.Hubs
{
    /// <summary>
    /// Provides a SignalR hub for real-time notifications related to document processing. Enables authenticated clients
    /// to subscribe to and receive updates for specific documents.
    /// </summary>
    [Authorize]
    public class DocumentHub : Hub
    {
        public async Task SubscribeToDocument(string documentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, documentId);
        }

        public async Task UnsubscribeFromDocument(string documentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentId);
        }
    }
}
