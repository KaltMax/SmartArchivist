import { useEffect, useState } from "react";
import PropTypes from "prop-types";
import { toast } from "react-toastify";
import { getAllDocuments } from "../api/DocumentGetService";
import { formatBytes } from "../utils/formatBytes";

function Statistics({ refreshTrigger }) {
  const [stats, setStats] = useState({
    totalDocuments: 0,
    totalStorage: 0,
    today: 0,
    thisWeek: 0,
  });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const docs = await getAllDocuments();

        const now = new Date();
        const startOfToday = new Date(
          now.getFullYear(),
          now.getMonth(),
          now.getDate()
        );
        const oneWeekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);

        const totalDocuments = docs.length;
        const totalStorage = docs.reduce(
          (sum, doc) => sum + (doc.fileSize || 0),
          0
        );
        const today = docs.filter(
          (doc) => new Date(doc.uploadDate) >= startOfToday
        ).length;
        const thisWeek = docs.filter(
          (doc) => new Date(doc.uploadDate) >= oneWeekAgo
        ).length;

        setStats({ totalDocuments, totalStorage, today, thisWeek });
      } catch (error) {
        console.error("Failed to load statistics:", error);
        toast.error(error.message || "Failed to load statistics");
      } finally {
        setLoading(false);
      }
    })();
  }, [refreshTrigger]);

  if (loading) {
    return (
      <div className="bg-[#010409] p-6 rounded-lg border border-gray-800 shadow-lg">
        <h2 className="text-2xl font-bold text-white mb-4">Statistics</h2>
        <div className="text-gray-400">Loading...</div>
      </div>
    );
  }

  return (
    <div className="bg-[#010409] p-6 rounded-lg border border-gray-800 shadow-lg">
      <h2 className="text-2xl font-bold text-white mb-6">Statistics</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4">
          <div className="text-gray-400 text-sm mb-1">Total Documents</div>
          <div className="text-3xl font-bold text-white">
            {stats.totalDocuments}
          </div>
        </div>
        <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4">
          <div className="text-gray-400 text-sm mb-1">Total Storage</div>
          <div className="text-3xl font-bold text-white">
            {formatBytes(stats.totalStorage)}
          </div>
        </div>
        <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4">
          <div className="text-gray-400 text-sm mb-1">Today</div>
          <div className="text-3xl font-bold text-green-400">
            +{stats.today}
          </div>
        </div>
        <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4">
          <div className="text-gray-400 text-sm mb-1">This Week</div>
          <div className="text-3xl font-bold text-blue-400">
            +{stats.thisWeek}
          </div>
        </div>
      </div>
    </div>
  );
}

Statistics.protoTypes = {
  refreshTrigger: PropTypes.number.isRequired,
};

export default Statistics;
