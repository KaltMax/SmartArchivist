import { useEffect } from 'react';
import { ToastContainer } from 'react-toastify';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { initializeAuth } from './api/AuthService';
import { NotificationProvider } from './contexts/NotificationProvider';
import Header from './components/Header';
import Dashboard from './components/Dashboard';
import Help from './components/Help';
import Sidebar from './components/Sidebar';
import DocumentList from './components/DocumentList';
import DocumentView from './components/DocumentView';

function App() {
  useEffect(() => {
    initializeAuth().catch((error) => {
      console.error('Failed to initialize auth:', error);
    });
  }, []);

  return (
    <NotificationProvider>
      <BrowserRouter>
        <div className="min-h-screen flex flex-col bg-[#1F1F1F]">
          <Header/>
          {/* Main area: Sidebar + Content */}
          <div className="flex flex-1 w-full overflow-x-hidden">
            <Sidebar />
            <main className="flex-1 min-w-0 p-4 md:p-8">
              <Routes>
                <Route path="/" element={<Dashboard />} />
                <Route path="/documents" element={<DocumentList />} />
                <Route path="/documents/:id" element={<DocumentView />} />
                <Route path="/help" element={<Help />} />
              </Routes>
            </main>
          </div>
          <ToastContainer position="bottom-left" theme="dark" />
        </div>
      </BrowserRouter>
    </NotificationProvider>
  )
}

export default App