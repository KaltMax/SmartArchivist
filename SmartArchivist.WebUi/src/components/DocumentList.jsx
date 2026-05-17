import { useEffect, useState, useMemo, useRef } from 'react';
import { toast } from 'react-toastify';
import { getAllDocuments } from '../api/DocumentGetService';
import { usePersistedState } from '../hooks/usePersistedState';
import { useClickOutside } from '../hooks/useClickOutside';
import DocumentCard from './DocumentCard';
import {
  ArrowsUpDownIcon,
  XMarkIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  EyeIcon,
} from '@heroicons/react/24/outline';

function DocumentList() {
  const [docs, setDocs] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showSortDropdown, setShowSortDropdown] = useState(false);
  const [showDisplayDropdown, setShowDisplayDropdown] = useState(false);
  const sortDropdownRef = useRef(null);
  const displayDropdownRef = useRef(null);

  const [sortConfig, setSortConfig] = usePersistedState('documentList.sortConfig', {
    sortBy: 'uploadDate',
    sortOrder: 'desc',
  });

  const [displayConfig, setDisplayConfig] = usePersistedState('documentList.displayConfig', {
    state: true,
    uploadDate: true,
    fileSize: true,
    fileExtension: false,
    contentType: false,
    tags: true,
  });

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const data = await getAllDocuments();
        setDocs(data);
      } catch (error) {
        console.error('Failed to load documents:', error);
        toast.error(error || 'Failed to load documents');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  useClickOutside(sortDropdownRef, () => setShowSortDropdown(false));
  useClickOutside(displayDropdownRef, () => setShowDisplayDropdown(false));

  // Apply sorting
  const sortedDocs = useMemo(() => {
    let result = [...docs];

    result.sort((a, b) => {
      let comparison = 0;

      switch (sortConfig.sortBy) {
        case 'name':
          comparison = a.name.localeCompare(b.name);
          break;
        case 'uploadDate':
          comparison = new Date(a.uploadDate) - new Date(b.uploadDate);
          break;
        case 'fileSize':
          comparison = a.fileSize - b.fileSize;
          break;
        case 'fileExtension':
          comparison = a.fileExtension.localeCompare(b.fileExtension);
          break;
        case 'state':
          comparison = a.state - b.state;
          break;
        case 'contentType':
          comparison = a.contentType.localeCompare(b.contentType);
          break;
      }

      return sortConfig.sortOrder === 'asc' ? comparison : -comparison;
    });

    return result;
  }, [docs, sortConfig.sortBy, sortConfig.sortOrder]);

  const toggleDisplayField = (fieldName) => {
    setDisplayConfig((prev) => ({
      ...prev,
      [fieldName]: !prev[fieldName],
    }));
  };

  const setSortBy = (sortBy) => {
    setSortConfig((prev) => ({ ...prev, sortBy }));
  };

  const toggleSortOrder = () => {
    setSortConfig((prev) => ({
      ...prev,
      sortOrder: prev.sortOrder === 'asc' ? 'desc' : 'asc',
    }));
  };

  const getSortLabel = (key) => {
    const labels = {
      name: 'Name',
      state: 'Processing State',
      uploadDate: 'Upload Date',
      fileSize: 'File Size',
      fileExtension: 'File Type',
      tags: 'Tags',
      contentType: 'Content Type',
    };
    return labels[key] || key;
  };

  const sortOptions = [
    'name',
    'state',
    'uploadDate',
    'fileSize',
    'fileExtension',
    'contentType',
    'tags',
  ];

  const renderContent = () => {
    if (loading) {
      return <div className="text-gray-300">Loading…</div>;
    }
    if (sortedDocs.length === 0) {
      return <div className="text-gray-400">No documents found.</div>;
    }
    return (
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
        {sortedDocs.map((d) => (
          <DocumentCard key={d.id} doc={d} displayConfig={displayConfig} />
        ))}
      </div>
    );
  };

  return (
    <div className="flex flex-col gap-4">
      {/* Header with Show and Sort dropdowns */}
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-white">Documents</h1>
        <div className="flex items-center gap-3">
          <div className="text-sm text-gray-400">{sortedDocs.length} documents</div>

          {/* Show Dropdown */}
          <div className="relative" ref={displayDropdownRef}>
            <button
              onClick={() => setShowDisplayDropdown(!showDisplayDropdown)}
              className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-gray-800 border border-gray-700 text-white text-sm hover:bg-gray-700 transition-colors"
            >
              <EyeIcon className="h-4 w-4" />
              Show
              <ChevronDownIcon className="h-3 w-3" />
            </button>

            {showDisplayDropdown && (
              <div className="absolute right-0 mt-2 w-64 bg-[#0B0F14] border border-gray-700 rounded-lg shadow-xl z-10 overflow-hidden">
                {/* Display Options with Checkboxes */}
                <div className="py-2">
                  {Object.keys(displayConfig).map((key) => (
                    <label
                      key={key}
                      className="flex items-center px-4 py-2 text-sm cursor-pointer text-gray-300 hover:bg-gray-800/50 transition-colors"
                    >
                      <input
                        type="checkbox"
                        checked={displayConfig[key]}
                        onChange={() => toggleDisplayField(key)}
                        className="mr-3 h-4 w-4 rounded border-gray-600 bg-gray-700 text-emerald-600 focus:ring-2 focus:ring-emerald-500 focus:ring-offset-0"
                      />
                      <span className="flex-1">{getSortLabel(key)}</span>
                    </label>
                  ))}
                </div>

                {/* Show All Fields */}
                <div className="border-t border-gray-700">
                  <button
                    onClick={() => {
                      setDisplayConfig(
                        Object.keys(displayConfig).reduce((acc, key) => {
                          acc[key] = true;
                          return acc;
                        }, {})
                      );
                    }}
                    className="w-full flex items-center justify-center gap-2 px-4 py-2.5 text-sm text-gray-400 hover:text-white hover:bg-gray-800 transition-colors"
                  >
                    <XMarkIcon className="h-4 w-4" />
                    Show All Fields
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Sort Dropdown */}
          <div className="relative" ref={sortDropdownRef}>
            <button
              onClick={() => setShowSortDropdown(!showSortDropdown)}
              className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-gray-800 border border-gray-700 text-white text-sm hover:bg-gray-700 transition-colors"
            >
              <ArrowsUpDownIcon className="h-4 w-4" />
              Sort
              <ChevronDownIcon className="h-3 w-3" />
            </button>

            {showSortDropdown && (
              <div className="absolute right-0 mt-2 w-64 bg-[#0B0F14] border border-gray-700 rounded-lg shadow-xl z-10 overflow-hidden">
                {/* Ascending/Descending Buttons */}
                <div className="flex border-b border-gray-700">
                  <button
                    onClick={toggleSortOrder}
                    className={`flex-1 flex items-center justify-center gap-2 px-4 py-2.5 text-sm font-medium transition-colors ${
                      sortConfig.sortOrder === 'asc'
                        ? 'bg-emerald-600 text-white'
                        : 'bg-gray-800 text-gray-300 hover:bg-gray-700'
                    }`}
                  >
                    <ChevronUpIcon className="h-4 w-4" />
                    Ascending
                  </button>
                  <button
                    onClick={toggleSortOrder}
                    className={`flex-1 flex items-center justify-center gap-2 px-4 py-2.5 text-sm font-medium transition-colors border-l border-gray-700 ${
                      sortConfig.sortOrder === 'desc'
                        ? 'bg-emerald-600 text-white'
                        : 'bg-gray-800 text-gray-300 hover:bg-gray-700'
                    }`}
                  >
                    <ChevronDownIcon className="h-4 w-4" />
                    Descending
                  </button>
                </div>

                {/* Sort Options */}
                <div className="py-2">
                  {sortOptions.map((key) => (
                    <button
                      key={key}
                      onClick={() => setSortBy(key)}
                      className={`w-full flex items-center justify-between px-4 py-2 text-sm transition-colors ${
                        sortConfig.sortBy === key
                          ? 'bg-emerald-600 text-white'
                          : 'text-gray-300 hover:bg-gray-800/50'
                      }`}
                    >
                      <span>{getSortLabel(key)}</span>
                      {sortConfig.sortBy === key && (
                        <span className="ml-2">{sortConfig.sortOrder === 'asc' ? '↑' : '↓'}</span>
                      )}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {renderContent()}
    </div>
  );
}

export default DocumentList;
