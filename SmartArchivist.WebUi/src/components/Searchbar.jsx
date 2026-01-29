import { useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { MagnifyingGlassIcon, XMarkIcon } from '@heroicons/react/24/outline';
import { toast } from 'react-toastify';
import { searchDocuments } from '../api/DocumentSearchService';

function Searchbar({ debounceMs = 400, onResults }) {
  const [query, setQuery] = useState('');
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const requestIdRef = useRef(0);
  const containerRef = useRef(null);
  const navigate = useNavigate();

  useEffect(() => {
    const onDocClick = (e) => {
      if (!containerRef.current?.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  useEffect(() => {
    const q = query.trim();
    if (q.length < 3) {
      setItems([]);
      onResults?.([]);
      setOpen(false);
      return;
    }

    const id = ++requestIdRef.current;
    const t = setTimeout(async () => {
      setLoading(true);
      try {
        const data = await searchDocuments(q);
        if (id === requestIdRef.current) {
          const list = Array.isArray(data) ? data : [];
          setItems(list);
          onResults?.(list);
          setOpen(true);
        }
      } catch (error) {
        console.error('Search error:', error);
        if (id === requestIdRef.current) {
          setItems([]);
          onResults?.([]);
          toast.error(error.message ||'Failed to search documents');
        }
      } finally {
        if (id === requestIdRef.current) setLoading(false);
      }
    }, debounceMs);

    return () => clearTimeout(t);
  }, [query, debounceMs, onResults]);

  const clear = () => {
    setQuery('');
    setItems([]);
    onResults?.([]);
    setOpen(false);
  };

  const renderDropdownContent = () => {
    if (loading) {
      return <div className="px-4 py-3 text-gray-400 text-sm">Searching...</div>;
    }
    if (items.length === 0) {
      return <div className="px-4 py-3 text-gray-400 text-sm">No results found</div>;
    }
    return (
      <ul className="max-h-72 overflow-auto divide-y divide-gray-800">
        {items.map((d) => {
          const title = d?.name ?? d?.title ?? d?.fileName ?? 'Document';
          return (
            <li key={d?.id ?? title}>
              <button
                type="button"
                className="w-full px-4 py-2 text-left text-sm text-gray-200 hover:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500"
                onClick={() => {
                  if (d?.id) {
                    setOpen(false);
                    navigate(`/documents/${d.id}`);
                  }
                }}
                title={title}
                aria-label={`Open ${title}`}
              >
                {title}
              </button>
            </li>
          );
        })}
      </ul>
    );
  };

  return (
    <div ref={containerRef} className="relative w-full md:w-[28rem]">
      <MagnifyingGlassIcon className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-gray-400" />
      <input
        type="text"
        value={query}
        onFocus={() => items.length && setOpen(true)}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Document Search…"
        aria-label="Search"
        className="w-full rounded-full bg-[#0B0F14] border border-gray-700 pl-10 pr-10 py-2 text-sm text-white placeholder-gray-400 outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
      />
      {query?.length > 0 && (
        <button
          type="button"
          onClick={clear}
          aria-label="Clear search"
          className="absolute right-2 top-1/2 -translate-y-1/2 p-1 rounded-full text-gray-400 hover:text-gray-200 hover:bg-gray-700/60"
        >
          <XMarkIcon className="h-5 w-5" />
        </button>
      )}

      {open && (
        <div className="absolute z-10 mt-2 w-full rounded-lg border border-gray-700 bg-[#0B0F14] shadow-xl overflow-hidden">
          {renderDropdownContent()}
        </div>
      )}
    </div>
  );
}

Searchbar.propTypes = {
  debounceMs: PropTypes.number,
  onResults: PropTypes.func,
};

export default Searchbar