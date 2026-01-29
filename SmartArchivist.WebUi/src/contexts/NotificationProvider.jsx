import { useCallback, useEffect, useRef, useMemo } from 'react';
import PropTypes from 'prop-types';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { toast } from 'react-toastify';
import { NotificationContext } from './NotificationContext';
import { getToken } from '../api/AuthService';

// Provider component that establishes SignalR connection and provides notification functions
export function NotificationProvider({ children }) {
    const connectionRef = useRef(null);

    useEffect(() => {
        // Get JWT token for authentication
        const token = getToken();

        if (!token) {
            console.error('No JWT token available for SignalR connection');
            toast.error('Authentication required for real-time notifications');
            return;
        }

        // Build SignalR connection with JWT token in query string
        // WebSocket connections cannot send Authorization headers, so we use query string
        const connection = new HubConnectionBuilder()
            .withUrl('/hubs/documents', {
                accessTokenFactory: () => token,
                withCredentials: true
            })
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Warning)
            .build();

        // Set up event listeners
        connection.on('DocumentProcessingCompleted', (data) => {
            console.log('Document processing completed:', data);
            const documentName = data.fileName || 'Unknown document';
            toast.success(`OCR, GenAI and Indexing Processing completed for "${documentName}"!`);
        });

        connection.on('DocumentProcessingFailed', (data) => {
            console.error('Document processing failed:', data);
            const documentName = data.fileName || 'Unknown document';
            const stage = data.stage || 'processing';

            // This notification fires when retries are exhausted and document is deleted
            toast.error(
                `Processing failed for "${documentName}" at ${stage} stage. ` +
                `Document has been removed. Please upload again.`,
                { autoClose: 8000 }
            );
        });

        // Start the connection
        connection.start()
            .then(() => {
                console.log('SignalR connected successfully');
            })
            .catch((error) => {
                console.error('SignalR connection error:', error);
                toast.error('Failed to connect to notification service');
            });

        // Store connection reference
        connectionRef.current = connection;

        // Cleanup on unmount
        return () => {
            if (connectionRef.current) {
                connectionRef.current.stop()
                    .then(() => console.log('SignalR disconnected'))
                    .catch((error) => console.error('Error disconnecting:', error));
            }
        };
    }, []);

    // Subscribe to updates for a specific document
    const subscribeToDocument = useCallback(async (documentId) => {
        if (!connectionRef.current) {
            console.error('SignalR connection not established yet');
            return;
        }

        try {
            await connectionRef.current.invoke('SubscribeToDocument', documentId.toString());
            console.log(`Subscribed to document: ${documentId}`);
        } catch (error) {
            console.error('Error subscribing to document:', error);
        }
    }, []);

    const contextValue = useMemo(
        () => ({ subscribeToDocument, connection: connectionRef.current }),
        [subscribeToDocument]
    );

    return (
        <NotificationContext.Provider value={contextValue}>
            {children}
        </NotificationContext.Provider>
    );
}

NotificationProvider.propTypes = {
    children: PropTypes.node.isRequired
};
