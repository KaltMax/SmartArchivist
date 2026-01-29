SmartArchivist
===============

A Document management system for archiving documents in a FileStore, with automatic OCR (queue for OC-recognition), automatic summary and tag generation using GenAI (Google Gemini), and full text search (ElasticSearch).

# Prerequisites

## To Run the Application (Docker-based)
- Git
- Docker (Docker Desktop for Windows/Mac or Docker Engine for Linux)
- Docker Compose

**Note:** Node.js and .NET SDK are NOT required on your host machine when using Docker, as all building happens inside containers.

## For Local Development
- Node.js v18 or higher (includes npm for building the React frontend)
- .NET 8.0 SDK (for building the backend services)

# Getting Started

1. Pull the project from GitHub and navigate to the project directory:

```
git clone https://github.com/KaltMax/SmartArchivist.git
cd SmartArchivist
```

2. Configuration is managed via the `.env` file in the project root. Default credentials are already configured for development/testing (should be removed for production). You can modify these values in the `.env` file if needed.
3. Ensure you have Docker and Docker Compose installed on your machine.
4. Start Docker Desktop if it's not already running.
5. Build and start the Docker containers using Docker Compose:

```bash
# Development mode (with direct backend port access)
docker compose up -d

# Production mode (only nginx on port 80)
docker compose -f docker-compose.yml up -d
```

6. Access the application via nginx reverse proxy (recommended):

- **Web UI**: `http://localhost/`
- **SmartArchivist API**: `http://localhost/api/`

**Development mode only** - Direct backend access (bypassing nginx):
- **SmartArchivist API**: `http://localhost:8081`
- **Swagger UI**: `http://localhost:8081/swagger/index.html` (only available in development mode)
- **Adminer**: `http://localhost:9091` (System: PostgreSQL, Server: postgres, Username: postgres, Password: postgres, Database: smartarchivist)
- **MinIO Console**: `http://localhost:9090` (Username: minioadmin, Password: minioadmin)
- **RabbitMQ Management**: `http://localhost:9093`
- **Elasticsearch**: `http://localhost:9200`
- **Kibana**: `http://localhost:9092`
- **PostgreSQL**: `localhost:5432`

7. Test the application by using the Swagger UI.

**Note for Visual Studio Users:**

The project includes native Tesseract OCR libraries in `SmartArchivist.Ocr/x64/` to support Visual Studio's Docker Compose debugging mode. When Visual Studio runs Docker Compose in debug mode, it mounts the local project directory into the container, which would otherwise overwrite the symlinks created during the Docker build process.

**Included Libraries:**
- `libleptonica-1.82.0.so` - Leptonica image processing library
- `libtesseract50.so` - Tesseract OCR engine library

These binary files are tracked in git to ensure the OCR worker functions correctly when debugging through Visual Studio. The application works identically whether started via Visual Studio's Docker Compose project or the command line.

# Project Structure

## Core Projects

| Project                     | Type                 | Description                                                            | Port |
| --------------------------- | -------------------- | ---------------------------------------------------------------------- | ---- |
| **SmartArchivist.WebUi**          | React + nginx        | Frontend application serving the user interface                        | 80   |
| **SmartArchivist.Api**           | ASP.NET Core Web API | RESTful API endpoints for client communication                         | 8081 |
| **SmartArchivist.Application**             | Class Library        | Business logic layer handling core operations                          | -    |
| **SmartArchivist.Dal**            | Class Library        | Data access layer with Entity Framework & repositories                 | -    |
| **SmartArchivist.Ocr**            | Worker Service       | Background worker services for async ocr processing                    | 8082 |
| **SmartArchivist.GenAi**          | Worker Service       | Background worker services for async GenAI summary and tag generation  | 8083 |
| **SmartArchivist.Indexing**       | Worker Service       | Background worker for async Elasticsearch document indexing            | 8084 |
| **SmartArchivist.Infrastructure** | Class Library        | Infrastructure implementations (RabbitMQ, MinIO, ElasticSearch)        | -    |
| **SmartArchivist.Contract**       | Class Library        | Shared contracts, DTOs, abstractions/interfaces, and logger            | -    |
| **SmartArchivist.Batch**          | Console Application  | Batch processing job for daily document backups                        | -    |
| **Tests**                   | xUnit Test Project   | Comprehensive test suite (unit, integration, E2E)                      | -    |

## Project Dependencies

- SmartArchivist.Api ─► SmartArchivist.Application + SmartArchivist.Dal + SmartArchivist.Contract + SmartArchivist.Infrastructure
- SmartArchivist.Application ─► SmartArchivist.Dal + SmartArchivist.Contract
- SmartArchivist.Dal ─► SmartArchivist.Contract (for logging)
- SmartArchivist.Infrastructure ─► SmartArchivist.Contract (for abstractions)
- **SmartArchivist.Ocr** ─► SmartArchivist.Dal + SmartArchivist.Contract + SmartArchivist.Infrastructure 
- **SmartArchivist.GenAi** ─► SmartArchivist.Dal + SmartArchivist.Contract + SmartArchivist.Infrastructure
- **SmartArchivist.Indexing** ─► SmartArchivist.Dal + SmartArchivist.Contract + SmartArchivist.Infrastructure
- SmartArchivist.Contract ─► no dependencies (base layer with abstractions)

## Communication Flow

```
UI → SmartArchivist.Api → Application → Filesystem/MinIO (store file)
                            ↓
                           Dal → PostgreSQL (save metadata, State: Uploaded)
                            ↓
              RabbitMQ (smartarchivist.ocr) → SmartArchivist.Ocr → Filesystem/MinIO (retrieve file)
                                              ↓
                                         OCR Processing
                                              ↓
                                           Dal → PostgreSQL (save OCR text, State: OcrCompleted)
                                              ↓
                                    RabbitMQ (smartarchivist.genai) → SmartArchivist.GenAi → GenAI API
                                                                          ↓
                                                                  GenAI Processing
                                                                          ↓
                                                                        Dal → PostgreSQL (save summary + tags, State: GenAiCompleted)
                                                                          ↓
                                                          RabbitMQ (smartarchivist.indexing) → SmartArchivist.Indexing → ElasticSearch (index document)
                                                                                                  ↓
                                                                                                Dal → PostgreSQL (State: Indexed)
                                                                                                  ↓
                                                                                    RabbitMQ (smartarchivist.document.result)
                                                                                                  ↓
                                                                                          DocumentResultWorker (in SmartArchivist.Api)
                                                                                                  ↓
                                                                                                Dal → PostgreSQL (State: Completed)
                                                                                                  ↓
                                                                                        SignalR → WebUI (notification)
```

## Document Upload Flow

1. UI → SmartArchivist.Api
2. Api → Application
3. Application → MinIO (store PDF)
4. Application → Dal → PostgreSQL (save metadata with State: Uploaded)
5. Application → RabbitMQ (publish `DocumentUploadedMessage` to `smartarchivist.ocr` queue)

### Async Processing Flow

6. RabbitMQ → OcrWorker
7. OcrWorker → Filesystem/MinIO (get PDF)
8. OcrWorker → OCR Engine (extract text)
9. OcrWorker → IDocumentRepository → PostgreSQL (save OCR text, update State: OcrCompleted)
10. OcrWorker → RabbitMQ (publish `OcrCompletedMessage` to `smartarchivist.genai` queue)
11. RabbitMQ → GenAIWorker
12. GenAIWorker → Gemini API (generate summary + tags)
13. GenAIWorker → IDocumentRepository → PostgreSQL (save GenAI summary + tags, update State: GenAiCompleted)
14. GenAIWorker → RabbitMQ (publish `GenAiCompletedMessage` to `smartarchivist.indexing` queue)
15. RabbitMQ → IndexingWorker
16. IndexingWorker → ElasticSearch (index document with text, summary, tags)
17. IndexingWorker → IDocumentRepository → PostgreSQL (update State: Indexed)
18. IndexingWorker → RabbitMQ (publish `IndexingCompletedMessage` to `smartarchivist.document.result` queue)
19. RabbitMQ → DocumentResultWorker (in SmartArchivist.Api)
20. DocumentResultWorker → IDocumentRepository → PostgreSQL (update State: Completed)
21. DocumentResultWorker → SignalR → WebUI (real-time notification)

## Document Search Flow

1. UI → User enters search query (minimum 3 characters)
2. UI → SmartArchivist.Api (GET /api/documents/search?query=...)
3. Api → Application → ISearchService → ElasticSearchService
4. ElasticSearchService → Multi-strategy search (fuzzy + wildcard matching) across fileName, extractedText, summary, tags
5. ElasticSearch → Returns matching document IDs ranked by relevance
6. Application → IDocumentRepository → PostgreSQL (fetch document metadata for IDs, excluding heavy fields)
7. Dal → Application (documents with metadata + summaries, but excluding OcrText and GenAiSummary for performance)
8. Application → Api (formatted search results)
9. Api → UI (JSON response)
10. UI → Display results to user with highlighting and relevance

## RabbitMQ Queue Architecture

The application uses four RabbitMQ queues for async processing:

| Queue Name | Producer | Consumer | Message Type | Purpose |
|------------|----------|----------|--------------|---------|
| `smartarchivist.ocr` | SmartArchivist.Api (DocumentService) | SmartArchivist.Ocr (OcrWorker) | `DocumentUploadedMessage` | Triggers OCR processing after document upload |
| `smartarchivist.genai` | SmartArchivist.Ocr (OcrWorker) | SmartArchivist.GenAi (GenAiWorker) | `OcrCompletedMessage` | Triggers GenAI summary and tag generation after OCR completion |
| `smartarchivist.indexing` | SmartArchivist.GenAi (GenAiWorker) | SmartArchivist.Indexing (IndexingWorker) | `GenAiCompletedMessage` | Triggers Elasticsearch indexing after GenAI completion |
| `smartarchivist.document.result` | SmartArchivist.Indexing (IndexingWorker) | SmartArchivist.Api (DocumentResultWorker) | `IndexingCompletedMessage` | Marks document as Completed and notifies WebUI |

### Queue Configuration
- All queues are durable (survive broker restarts)
- Manual acknowledgment with message requeue on failure
- QoS prefetch count = 1 (fair distribution across consumers)
- Messages serialized as JSON with camelCase naming

### Error Handling & Retry Logic

**Automatic Retry Mechanism:**
- Failed messages are automatically retried up to **2 times**
- Retry count tracked in message header (`x-retry-count`)
- After max retries exceeded, messages moved to **Dead Letter Queue (DLQ)**

**Dead Letter Queues (DLQ):**

| DLQ Name | Purpose | Handler |
|----------|---------|---------|
| `smartarchivist.ocr.dlq` | Failed OCR processing messages | FailedDocumentProcessingHandler |
| `smartarchivist.genai.dlq` | Failed GenAI processing messages | FailedDocumentProcessingHandler |
| `smartarchivist.indexing.dlq` | Failed indexing messages | FailedDocumentProcessingHandler |
| `smartarchivist.document.result.dlq` | Failed document completion messages | FailedDocumentProcessingHandler |

**Rollback Behavior:**
- When a message reaches a DLQ (after 3 failed retries), the `FailedDocumentProcessingHandler` performs automatic cleanup:
  1. Deletes the document from MinIO storage
  2. Removes the document metadata from PostgreSQL
  3. Sends a failure notification to the WebUI via SignalR
  4. Logs the failure

This ensures no orphaned documents remain in the system after processing failures.

### Message Types

- DocumentUploadedMessage
- OcrCompletedMessage
- GenAiCompletedMessage
- IndexingCompletedMessage

## Dockerfiles

- SmartArchivist.WebUi (Port 80)
- SmartArchivist.Api (Port 8081)
- SmartArchivist.Ocr (Port 8082 - health check endpoint)
- SmartArchivist.GenAi (Port 8083 - health check endpoint)
- SmartArchivist.Indexing (Port 8084 - health check endpoint)

## Docker-Container in compose.yaml

- SmartArchivist.WebUi (Port 80)
- SmartArchivist.Api (Port 8081)
- SmartArchivist.Ocr (Port 8082 - health check endpoint)
- SmartArchivist.GenAi (Port 8083 - health check endpoint)
- SmartArchivist.Indexing (Port 8084 - health check endpoint)
- PostgreSQL (Port 5432)
- Adminer (Port 9091)
- RabbitMQ (Port 5672 + Console Port 9093)
- MinIO (Port 9000 + Console Port 9090)
- ElasticSearch (Port 9200)
- Elasticsearch UI (Port 9092)

# Infrastructure Services

## Application Services

- **SmartArchivist.WebUi**: `http://localhost:80` - Main web interface
- **SmartArchivist.Api**: `http://localhost:8081` - API endpoints
- **SmartArchivist.Ocr**: `http://localhost:8082` - OCR worker service (health check endpoint)
- **SmartArchivist.GenAi**: `http://localhost:8083` - GenAI worker service (health check endpoint)
- **SmartArchivist.Indexing**: `http://localhost:8084` - Indexing worker service (health check endpoint)

## Data & Storage Services

- **PostgreSQL**: `localhost:5432` - Primary database
- **MinIO**: `localhost:9000` - Object storage for documents
- **ElasticSearch**: `localhost:9200` - Full-text search engine
- **RabbitMQ**: `localhost:5672` - Message queue for async processing

## Management Interfaces

- **Adminer**: `http://localhost:9091` - Database administration
- **MinIO Console**: `http://localhost:9090` - File storage management (Username: minioadmin, Password: minioadmin)
- **RabbitMQ Management**: `http://localhost:9093` - Queue monitoring
- **Elasticsearch UI (Kibana)**: `http://localhost:9092` - Search index management

# Projects Overview

## SmartArchivist.WebUi

React-based frontend application providing:

- Document upload with drag & drop support
- Real-time processing notifications via SignalR (WebSocket)
- Document browsing with GenAI-generated tag display
- Search functionality for finding documents
- Sorting of documents by metadata fields
- Inline editing of document name and summary with automatic search index updates
- PDF viewer with metadata, OCR content, and GenAI summary display
- Tag visualization in document cards and detail view
- Document management (view, download, delete)
- JWT authentication
- Responsive UI built with Tailwind CSS
- Full REST API integration via Axios

[Documentation of the WebUI](./SmartArchivist.WebUi/README.md)

## SmartArchivist.Api

ASP.NET Core Web API project providing RESTful endpoints for document management.

### Document Controller Endpoints

- `POST /api/documents/upload`: Upload a new document (multipart/form-data)
- `GET /api/documents`: Retrieve a list of all documents
- `GET /api/documents/{id}`: Retrieve metadata for a specific document by ID
- `PATCH /api/documents/{id}`: Update document metadata (name and/or summary) with automatic re-indexing
- `DELETE /api/documents/{id}`: Delete a specific document by ID
- `GET /api/documents/{id}/download`: Download the original document file
- `GET /api/documents/search?q={query}`: Search documents by text query

### Background Workers

**DocumentResultWorker:**
- Runs as a background service within SmartArchivist.Api
- Subscribes to `smartarchivist.document.result` RabbitMQ queue
- Consumes `GenAiCompletedMessage` after all processing is complete
- Updates document state to Completed in PostgreSQL via `IDocumentService`
- Sends real-time notifications to WebUI clients via SignalR

**Message Flow:**
1. Receives `GenAiCompletedMessage` from `smartarchivist.document.result` queue
2. Extracts DocumentId from message
3. Calls `IDocumentService.UpdateDocumentStateAsync(documentId, DocumentState.Completed)`
4. Updates DocumentEntity state to Completed in PostgreSQL
5. Notifies WebUI clients via SignalR with document completion event
6. Logs success/failure with detailed information

**Document State Management:**

Each worker directly updates the document state in the database to track processing progress:

| State | Description | Set By |
|-------|-------------|--------|
| `Uploaded` | Document uploaded, ready for OCR | DocumentService (during upload) |
| `OcrCompleted` | OCR text extraction completed | OcrWorker |
| `GenAiCompleted` | GenAI summary and tag generation completed | GenAiWorker |
| `Indexed` | Document indexed in Elasticsearch | IndexingWorker |
| `Completed` | All processing completed, ready for user | DocumentResultWorker |
| `Failed` | Processing failed at any stage | OcrWorker / GenAiWorker / IndexingWorker (on exception) |

**Error Handling:**
- If a worker encounters an error, it updates the document state to `Failed` before rethrowing the exception
- Failed messages are retried up to 2 times via RabbitMQ's automatic retry mechanism
- After max retries, messages are moved to Dead Letter Queues for manual investigation

## SmartArchivist.Application

Core business logic layer handling operations such as:

- Document upload and validation
- Document metadata updates (name, summary) with automatic re-indexing
- Business rule enforcement and workflow orchestration
- Interaction with infrastructure services (via abstractions):
  - File storage through `IFileStorageService` (MinIO)
  - Message publishing through `IRabbitMqPublisher`
  - Document search through `ISearchService` (ElasticSearch)
  - Search indexing through `IIndexingService` (ElasticSearch)
- Interaction with Dal for metadata management
- Model mappings

## SmartArchivist.Dal

Data access layer using Entity Framework Core for PostgreSQL.

- Code first approach with migrations
- Repository pattern for data access abstraction
- Entities: Document
- CRUD operations for Document metadata

## SmartArchivist.Ocr

Background worker service for asynchronous OCR document processing using Tesseract.

**OCR Components:**
- `TesseractOcrService`: Implements OCR using Tesseract 5 with trained data for multiple languages
- `MagickPdfToImageConverter`: Converts PDF pages to images using Magick.NET for OCR processing
- `OcrWorker`: Coordinates the OCR pipeline (fetch PDF → convert to images → extract text → publish results)

**Architecture:**
- Uses .NET `BackgroundService` for long-running tasks
- Subscribes to RabbitMQ queues via `IRabbitMqConsumer`
- Retrieves documents from MinIO via `IFileStorageService`
- Accesses database via `IDocumentRepository`
- Processes messages asynchronously with error handling
- Failed messages are automatically requeued for retry

**Message Flow:**
1. Document uploaded via SmartArchivist.Api
2. SmartArchivist.Api publishes `DocumentUploadedMessage` to `smartarchivist.ocr` queue
3. OcrWorker consumes message and retrieves PDF from MinIO
4. OcrWorker converts PDF pages to images using Magick.NET
5. OcrWorker extracts text from images using Tesseract OCR
6. OcrWorker publishes `OcrCompletedMessage` to `smartarchivist.genai` queue with extracted text

## SmartArchivist.GenAi

Background worker service for asynchronous AI-powered document summary and tag generation using Google Gemini.

**GenAI Components:**
- `GeminiGenAiSummaryService`: Implements document summarization and tagging using Google's Gemini generative AI API
- `GeminiRequestBuilder`: Builds structured JSON requests with system prompts and document content
- `GeminiResponseParser`: Parses Gemini API responses to extract summary and tags
- `GenAIWorker`: Coordinates the GenAI pipeline (consume OCR text → generate summary + tags → publish results)

**Architecture:**
- Uses .NET `BackgroundService` for long-running tasks
- Subscribes to RabbitMQ queues via `IRabbitMqConsumer`
- Accesses database via `IDocumentRepository`**
- Uses only required infrastructure: RabbitMQ and PostgreSQL
- Integrates with Gemini API via `IGenAiSummaryService`
- Requires configuration via appsettings.json or environment variables (`GenAi:ApiKey`, `GenAi:ApiUrl`, `GenAi:SystemPrompt`)
- Uses Gemini's structured output feature to ensure consistent JSON responses
- Processes messages asynchronously with error handling
- Failed messages are automatically requeued for retry

**GenAI Output:**
- **Summary**: 2-3 sentence concise summary of document content
- **Tags**: 3-5 relevant keywords for categorization and filtering

**Message Flow:**
1. OcrWorker publishes `OcrCompletedMessage` to `smartarchivist.genai` queue
2. GenAIWorker consumes message with extracted text
3. GenAIWorker sends text to Gemini API for summary and tag generation
4. GenAIWorker saves GenAI summary and tags to PostgreSQL and updates state to GenAiCompleted
5. GenAIWorker publishes `GenAiCompletedMessage` to `smartarchivist.document.result` queue
6. DocumentResultWorker (in SmartArchivist.Api) consumes message
7. DocumentResultWorker updates document state to Completed and notifies WebUI clients

## SmartArchivist.Contract

Shared contract library containing abstractions and data contracts:

**Abstractions (Interfaces):**
- Abstractions for messaging, storage, OCR, GenAI, search, and logging

**Data Transfer Objects (DTOs):**
- Document objects for API communication (including GenAI summaries and tags)
- GenAiResult (summary and tags array)
- Queue message definitions (DocumentUploadedMessage, OcrCompletedMessage, GenAiCompletedMessage)

**Constants:**
- RabbitMQ queue name constants
- Document-related constants

## SmartArchivist.Infrastructure

Infrastructure layer containing concrete implementations of external service integrations:

**Messaging (RabbitMQ):**
- RabbitMQ publisher and consumer implementations
- Message serialization using JSON

**Storage (MinIO):**
- Object storage implementation using MinIO
- Integrated with SmartArchivist.Ocr and SmartArchivist.Api
- Bucket creation and validation at startup
- File upload, download, and management

**Search (ElasticSearch):**
- `ElasticSearchService`: Full-text search with fuzzy and wildcard matching across fileName, extractedText, summary, tags
- `ElasticSearchIndexingService`: Document indexing and deletion operations
- Configurable via `ElasticSearch__Url`, `ElasticSearch__IndexName`, and `ElasticSearch__MaxSearchResults`
- Uses document ID as ElasticSearch `_id` for efficient retrieval
- Integrated with SmartArchivist.Api, SmartArchivist.Indexing, and SmartArchivist.Application

**Dependencies:** SmartArchivist.Infrastructure implements interfaces from SmartArchivist.Contract

## SmartArchivist.Batch

Console application for scheduled batch processing operations.

**Features:**
- Standalone console application using .NET Generic Host
- Designed to run via Windows Task Scheduler or cron
- Automated backups of all document metadata and files
- Exports complete document metadata (including OCR text and GenAI summaries) to JSON
- Downloads all PDF files from MinIO storage
- Generates backup manifest with statistics and error tracking
- Timestamped backup folders

**Backup Structure:**
```
Backups/
└── backup_20251217_124632/
    ├── documents-metadata.json    # Complete document metadata
    ├── backup-manifest.json       # Backup statistics
    └── files/                     # All PDF files
        ├── {guid}_{filename}.pdf
        └── ...
```

- [Full Batch Processing Documentation](./SmartArchivist.Batch/BATCH_PROCESSING.md)

## Tests

Test suite using **xUnit** as testing framework and **NSubstitute** for mocking dependencies.

**Unit Tests:**
- Test individual components in isolation (services, workers, controllers)
- Mock external dependencies (repositories, message brokers, external APIs)
- Cover business logic, validation, error handling, and edge cases

**Integration Tests:**
- Test workflows with real infrastructure (PostgreSQL, RabbitMQ via InMemoryMessageBroker)
- Verify document upload, processing pipeline, and database persistence
- Test worker message flow (OCR → GenAI → Indexing → Result)
- Use real PDF files
- Validate SignalR notifications and error handling
- Use `WebApplicationFactory` for realistic testing environment

**Test Coverage:**
- SmartArchivist.Application: Business logic and service layer
- SmartArchivist.Api: Controllers and background workers
- SmartArchivist.Ocr/GenAi/Indexing: Worker services and message handling

## How to Run Integration Tests

1. Ensure Docker is running
2. Navigate to project root: `cd SmartArchivist`
3. Run integration tests: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~IntegrationTests"`

# GitHub Workflows

- Main branch protection rules to enforce PR reviews and successful CI checks before merging from development
- Development branch for integrating features before merging into main
- Feature branches for individual tasks and features
- Pull Request from development into main after every sprint

# CI Pipeline

- GitHub Actions workflow for building and testing the solution on each push and pull request

## Jobs

### Backend
- Build backend projects (SmartArchivist.Api, SmartArchivist.Application, SmartArchivist.Dal, SmartArchivist.Ocr, SmartArchivist.GenAi, SmartArchivist.Indexing, SmartArchivist.Infrastructure, SmartArchivist.Contract)
- Run backend unit tests
- Run backend integration tests

### SmartArchivist.WebUi
- Lint React code using ESLint
- Build React application
- Run unit tests for WebUI