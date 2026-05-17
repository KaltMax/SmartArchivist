import { useEffect, useState } from 'react';

// Like useState, but persists the value to localStorage under `key`.
// The initial value is taken from localStorage if present, otherwise `initialValue`.
export function usePersistedState(key, initialValue) {
  const [value, setValue] = useState(() => {
    try {
      const saved = localStorage.getItem(key);
      return saved === null ? initialValue : JSON.parse(saved);
    } catch {
      return initialValue;
    }
  });

  useEffect(() => {
    localStorage.setItem(key, JSON.stringify(value));
  }, [key, value]);

  return [value, setValue];
}
