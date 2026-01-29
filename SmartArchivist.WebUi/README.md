# SmartArchivist.WebUi

Modern React-based frontend for the SmartArchivist document management system.

## Tech Stack

- **React** - UI framework
- **Vite** - Build tool and dev server
- **Tailwind CSS** - Styling
- **React Router** - Client-side routing
- **Axios** - HTTP client for API communication
- **SignalR** - Real-time WebSocket communication for document processing notifications
- **React Toastify** - Toast notifications
- **Vitest** - Unit testing framework

## Project Structure

```
src/
├── api/              # API service layer with validation
├── components/       # React components (Header, Sidebar, DocumentView, etc.)
├── contexts/         # React contexts (NotificationContext, NotificationProvider)
├── hooks/            # Custom React hooks (useNotifications)
├── utils/            # Utility functions (formatBytes, etc.)
└── App.jsx           # Main app with routing
```

## Configuration

The app connects to the backend via nginx reverse proxy:
- **REST API**: `/api` - HTTP requests for document management
- **SignalR Hub**: `/hubs/documents` - WebSocket connection for real-time notifications

Authentication uses JWT tokens managed by `AuthService`.

## Getting Started

### Prerequisites
- Node.js (recommended: v18+)

### Install Dependencies
```bash
npm install
```

### Run Development Server
```bash
npm run dev
```
Dev server runs on `http://localhost:5173`

### Run Tests
```bash
npm run test          # Run with coverage
npm run test:ui       # Run with UI
```

**Test Strategy:** Unit tests focus on business logic (validation, utilities).

### Lint the Code
```bash
npm run lint
```

### Build for Production
```bash
npm run build
```

### Preview Production Build
```bash
npm run preview
```

## Key Features

- **Document Upload** - Drag & drop interface with PDF validation
- **Real-time Notifications** - Live updates on document processing status via SignalR
  - Success/failure toast notifications
  - Document name displayed in notifications
- **Document Search** - Live search results as you type
- **Sorting** - Organize documents by filename, date, size, file type, etc.
- **Document Metadata Editing** - Inline editing of document name and summary
- **PDF Viewer** - Embedded document preview and download
- **JWT Authentication** - Secure token-based authentication
- **Responsive Design** - Mobile-friendly interface