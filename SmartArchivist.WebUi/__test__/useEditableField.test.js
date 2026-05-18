import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';

const toastSuccess = vi.fn();
const toastError = vi.fn();
vi.mock('react-toastify', () => ({
  toast: {
    success: (...args) => toastSuccess(...args),
    error: (...args) => toastError(...args),
  },
}));

const { useEditableField } = await import('../src/hooks/useEditableField');

describe('useEditableField', () => {
  beforeEach(() => {
    toastSuccess.mockClear();
    toastError.mockClear();
  });

  it('starts with isEditing=false and an empty draft', () => {
    const { result } = renderHook(() => useEditableField({ onSave: vi.fn() }));
    expect(result.current.isEditing).toBe(false);
    expect(result.current.editedValue).toBe('');
    expect(result.current.isSaving).toBe(false);
  });

  it('startEdit seeds the draft and flips isEditing', () => {
    const { result } = renderHook(() => useEditableField({ onSave: vi.fn() }));
    act(() => result.current.startEdit('hello'));
    expect(result.current.isEditing).toBe(true);
    expect(result.current.editedValue).toBe('hello');
  });

  it('cancel clears the draft and exits editing', () => {
    const { result } = renderHook(() => useEditableField({ onSave: vi.fn() }));
    act(() => result.current.startEdit('hello'));
    act(() => result.current.cancel());
    expect(result.current.isEditing).toBe(false);
    expect(result.current.editedValue).toBe('');
  });

  it('save calls onSave, exits editing, and toasts success', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const { result } = renderHook(() => useEditableField({ onSave, successMessage: 'Saved!' }));
    act(() => result.current.startEdit('new value'));
    await act(async () => {
      await result.current.save();
    });
    expect(onSave).toHaveBeenCalledWith('new value');
    expect(result.current.isEditing).toBe(false);
    expect(toastSuccess).toHaveBeenCalledWith('Saved!');
  });

  it('save aborts when validate returns an error and toasts it', async () => {
    const onSave = vi.fn();
    const { result } = renderHook(() => useEditableField({ onSave, validate: () => 'bad input' }));
    act(() => result.current.startEdit('x'));
    await act(async () => {
      await result.current.save();
    });
    expect(onSave).not.toHaveBeenCalled();
    expect(toastError).toHaveBeenCalledWith('bad input');
    expect(result.current.isEditing).toBe(true);
  });

  it('save keeps isEditing true and toasts on onSave failure', async () => {
    const onSave = vi.fn().mockRejectedValue(new Error('boom'));
    const { result } = renderHook(() => useEditableField({ onSave, errorMessage: 'Save failed' }));
    act(() => result.current.startEdit('x'));
    await act(async () => {
      await result.current.save();
    });
    expect(result.current.isEditing).toBe(true);
    expect(toastError).toHaveBeenCalledWith('boom');
  });

  it('uses errorMessage when the thrown error has no message', async () => {
    const onSave = vi.fn().mockRejectedValue({});
    const { result } = renderHook(() =>
      useEditableField({ onSave, errorMessage: 'Could not save' })
    );
    act(() => result.current.startEdit('x'));
    await act(async () => {
      await result.current.save();
    });
    expect(toastError).toHaveBeenCalledWith('Could not save');
  });

  it('toggles isSaving around the save call', async () => {
    let resolveOnSave;
    const onSave = vi.fn(() => new Promise((r) => (resolveOnSave = r)));
    const { result } = renderHook(() => useEditableField({ onSave }));
    act(() => result.current.startEdit('x'));

    let savePromise;
    act(() => {
      savePromise = result.current.save();
    });
    await waitFor(() => expect(result.current.isSaving).toBe(true));

    await act(async () => {
      resolveOnSave();
      await savePromise;
    });
    expect(result.current.isSaving).toBe(false);
  });
});
