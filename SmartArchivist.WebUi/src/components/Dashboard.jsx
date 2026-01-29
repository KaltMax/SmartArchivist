import { useState } from 'react';
import DocumentUpload from './UploadDocument';
import Statistics from './Statistics';

function Dashboard() {
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  // Trigger for refreshing statistics after a successful upload
  const handleUploadSuccess = () => {
    setRefreshTrigger(prev => prev + 1);
  };

  return (
    <div>
      <h1 className="text-2xl font-semibold text-white mb-4">Dashboard</h1>
      <DocumentUpload onUploadSuccess={handleUploadSuccess} />
      <Statistics refreshTrigger={refreshTrigger} />
    </div>
  )
}

export default Dashboard