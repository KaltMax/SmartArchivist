import { useCallback, useEffect, useRef, useMemo } from 'react';
import PropTypes from 'prop-types';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { toast } from 'react-toastify';
import { NotificationContext } from './NotificationContext';
import { getToken, refreshToken } from '../api/AuthService';

function registerNotificationHandlers(connection) {
    connection.on('DocumentProcessingCompleted', (data) => {
        const documentName = data.fileName || 'Unknown document';
        toast.success(`OCR, GenAI and Indexing Processing completed for "${documentName}"!`);
    });

    connection.on('DocumentProcessingFailed', (data) => {
        const documentName = data.fileName || 'Unknown document';
        const stage = data.stage || 'processing';
        toast.error(
            `Processing failed for "${documentName}" at ${stage} stage. ` +
            `Document has been removed. Please upload again.`,
            { autoClose: 8000 }
        );
    });
}

export function NotificationProvider({ children }) {
    const connectionRef = useRef(null);

    useEffect(() => {
        if (!getToken()) {
            toast.error('Authentication required for real-time notifications');
            return;
        }

        let cancelled = false;

        // Build, start, and self-heal the SignalR connection.
        // - Stale token at startup: start() fails, we refresh and retry.
        // - Token expires mid-session: auto-reconnect exhausts, onclose fires,
        //   we recursively call connect() which goes through the same path.
        const connect = async () => {
            if (cancelled) return;

            const connection = new HubConnectionBuilder()
                .withUrl('/hubs/documents', {
                    accessTokenFactory: () => getToken(),
                    withCredentials: true
                })
                .withAutomaticReconnect()
                .configureLogging(LogLevel.Warning)
                .build();

            registerNotificationHandlers(connection);

            // A graceful stop() passes no error; only react to real failures.
            connection.onclose((error) => {
                if (error && !cancelled) connect();
            });

            try {
                await connection.start();
            } catch {
                if (cancelled) return;
                await refreshToken();
                if (cancelled) return;
                await connection.start();
            }

            if (cancelled) {
                connection.stop().catch(() => {});
                return;
            }
            connectionRef.current = connection;
        };

        connect().catch((error) => {
            console.error('SignalR connection error:', error);
            toast.error('Failed to connect to notification service');
        });

        return () => {
            cancelled = true;
            connectionRef.current?.stop().catch(() => {});
            connectionRef.current = null;
        };
    }, []);

    const subscribeToDocument = useCallback(async (documentId) => {
        if (!connectionRef.current) {
            console.error('SignalR connection not established yet');
            return;
        }
        try {
            await connectionRef.current.invoke('SubscribeToDocument', documentId.toString());
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
