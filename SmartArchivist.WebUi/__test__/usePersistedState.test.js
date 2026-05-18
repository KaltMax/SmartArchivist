import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { usePersistedState } from '../src/hooks/usePersistedState';

describe('usePersistedState', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('returns initialValue when localStorage is empty', () => {
    const { result } = renderHook(() => usePersistedState('key', { a: 1 }));
    expect(result.current[0]).toEqual({ a: 1 });
  });

  it('hydrates from localStorage on mount', () => {
    localStorage.setItem('key', JSON.stringify({ a: 42 }));
    const { result } = renderHook(() => usePersistedState('key', { a: 1 }));
    expect(result.current[0]).toEqual({ a: 42 });
  });

  it('persists updates back to localStorage', () => {
    const { result } = renderHook(() => usePersistedState('key', 0));
    act(() => result.current[1](7));
    expect(result.current[0]).toBe(7);
    expect(JSON.parse(localStorage.getItem('key'))).toBe(7);
  });

  it('falls back to initialValue when stored JSON is corrupt', () => {
    localStorage.setItem('key', '{not valid json');
    const { result } = renderHook(() => usePersistedState('key', 'fallback'));
    expect(result.current[0]).toBe('fallback');
  });

  it('does not interfere across keys', () => {
    localStorage.setItem('keyA', JSON.stringify('alpha'));
    const { result } = renderHook(() => usePersistedState('keyB', 'beta'));
    expect(result.current[0]).toBe('beta');
  });

  it('supports the updater-function form of setState', () => {
    const { result } = renderHook(() => usePersistedState('counter', 1));
    act(() => result.current[1]((prev) => prev + 1));
    expect(result.current[0]).toBe(2);
    expect(JSON.parse(localStorage.getItem('counter'))).toBe(2);
  });
});
