import { useEffect, useId, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { MagnifyingGlassIcon, XMarkIcon } from '@heroicons/react/24/outline';
import { toast } from 'react-toastify';
import { searchDocuments } from '../api/DocumentSearchService';
import { useClickOutside } from '../hooks/useClickOutside';

function Searchbar({ debounceMs = 400, onResults }) {
  const [query, setQuery] = useState('');
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const requestIdRef = useRef(0);
  const containerRef = useRef(null);
  const itemRefs = useRef([]);
  const listboxId = useId();
  const navigate = useNavigate();

  useClickOutside(containerRef, () => setOpen(false));

  // Reset keyboard highlight whenever the results change or the dropdown closes
  useEffect(() => {
    setActiveIndex(-1);
  }, [items, open]);

  // Keep the highlighted result visible inside the scrollable list
  useEffect(() => {
    if (activeIndex >= 0) {
      itemRefs.current[activeIndex]?.scrollIntoView?.({ block: 'nearest' });
    }
  }, [activeIndex]);

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
          toast.error(error.message || 'Failed to search documents');
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

  const openDocument = (d) => {
    if (d?.id) {
      setOpen(false);
      void navigate(`/documents/${d.id}`);
    }
  };

  const handleKeyDown = (e) => {
    const hasResults = !loading && items.length > 0;

    switch (e.key) {
      case 'ArrowDown':
        if (!hasResults) return;
        e.preventDefault();
        if (open) {
          setActiveIndex((i) => (i + 1) % items.length);
        } else {
          setOpen(true);
        }
        break;
      case 'ArrowUp':
        if (!hasResults || !open) return;
        e.preventDefault();
        setActiveIndex((i) => (i <= 0 ? items.length - 1 : i - 1));
        break;
      case 'Enter':
        if (open && hasResults && activeIndex >= 0) {
          e.preventDefault();
          openDocument(items[activeIndex]);
        }
        break;
      case 'Escape':
        if (open) {
          e.preventDefault();
          setOpen(false);
        }
        break;
      default:
        break;
    }
  };

  const renderDropdownContent = () => {
    if (loading) {
      return <div className="px-4 py-3 text-gray-400 text-sm">Searching...</div>;
    }
    if (items.length === 0) {
      return <div className="px-4 py-3 text-gray-400 text-sm">No results found</div>;
    }
    return (
      <ul id={listboxId} role="listbox" className="max-h-72 overflow-auto divide-y divide-gray-800">
        {items.map((d, index) => {
          const title = d?.name ?? d?.title ?? d?.fileName ?? 'Document';
          const isActive = index === activeIndex;
          return (
            <li
              key={d?.id ?? title}
              id={`${listboxId}-option-${index}`}
              role="option"
              aria-selected={isActive}
              ref={(el) => (itemRefs.current[index] = el)}
            >
              <button
                type="button"
                className={`w-full px-4 py-2 text-left text-sm text-gray-200 focus:outline-none focus:ring-2 focus:ring-emerald-500 ${
                  isActive ? 'bg-gray-800' : ''
                }`}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => openDocument(d)}
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
        onKeyDown={handleKeyDown}
        placeholder="Document Search…"
        aria-label="Search"
        role="combobox"
        aria-autocomplete="list"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-activedescendant={
          open && activeIndex >= 0 ? `${listboxId}-option-${activeIndex}` : undefined
        }
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

export default Searchbar;
