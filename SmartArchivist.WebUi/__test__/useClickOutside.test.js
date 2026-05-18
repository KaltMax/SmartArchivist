import { describe, it, expect, vi, afterEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useRef } from 'react';
import { useClickOutside } from '../src/hooks/useClickOutside';

function fireMousedown(target) {
  target.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
}

describe('useClickOutside', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('fires the handler when mousedown happens outside the ref element', () => {
    const inside = document.createElement('div');
    const outside = document.createElement('div');
    document.body.append(inside, outside);

    const handler = vi.fn();
    renderHook(() => {
      const ref = useRef(inside);
      useClickOutside(ref, handler);
    });

    fireMousedown(outside);
    expect(handler).toHaveBeenCalledOnce();
  });

  it('does NOT fire when mousedown happens inside the ref element', () => {
    const inside = document.createElement('div');
    const child = document.createElement('span');
    inside.appendChild(child);
    document.body.append(inside);

    const handler = vi.fn();
    renderHook(() => {
      const ref = useRef(inside);
      useClickOutside(ref, handler);
    });

    fireMousedown(child);
    expect(handler).not.toHaveBeenCalled();
  });

  it('removes the listener on unmount', () => {
    const inside = document.createElement('div');
    const outside = document.createElement('div');
    document.body.append(inside, outside);

    const handler = vi.fn();
    const { unmount } = renderHook(() => {
      const ref = useRef(inside);
      useClickOutside(ref, handler);
    });

    unmount();
    fireMousedown(outside);
    expect(handler).not.toHaveBeenCalled();
  });

  it('uses the latest handler without re-wiring the listener', () => {
    const inside = document.createElement('div');
    const outside = document.createElement('div');
    document.body.append(inside, outside);

    const first = vi.fn();
    const second = vi.fn();

    const { rerender } = renderHook(
      ({ h }) => {
        const ref = useRef(inside);
        useClickOutside(ref, h);
      },
      { initialProps: { h: first } }
    );

    rerender({ h: second });
    fireMousedown(outside);
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledOnce();
  });
});
