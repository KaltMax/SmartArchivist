import { useEffect, useRef } from 'react';

// Fires `handler` when a mousedown occurs outside the element referenced by `ref`.
// The handler is stored in a ref so callers can pass inline arrow functions
// without re-wiring the document listener on every render.
export function useClickOutside(ref, handler) {
  const handlerRef = useRef(handler);
  handlerRef.current = handler;

  useEffect(() => {
    const onDocClick = (event) => {
      if (ref.current && !ref.current.contains(event.target)) {
        handlerRef.current(event);
      }
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [ref]);
}
