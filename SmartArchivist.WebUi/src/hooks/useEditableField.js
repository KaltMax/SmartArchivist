import { useState } from 'react';
import { toast } from 'react-toastify';

// Manages an editable field: edit-toggle, draft value, save with validation + toasts.
// `onSave(value)` performs the persistence step and any state updates on success;
// throwing from it signals failure (the message is surfaced via toast).
// `validate(value)` may return an error string to abort the save, or a falsy value to allow it.
export function useEditableField({ onSave, validate, successMessage, errorMessage }) {
  const [isEditing, setIsEditing] = useState(false);
  const [editedValue, setEditedValue] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const startEdit = (initialValue) => {
    setEditedValue(initialValue ?? '');
    setIsEditing(true);
  };

  const cancel = () => {
    setIsEditing(false);
    setEditedValue('');
  };

  const save = async () => {
    const validationError = validate?.(editedValue);
    if (validationError) {
      toast.error(validationError);
      return;
    }
    setIsSaving(true);
    try {
      await onSave(editedValue);
      setIsEditing(false);
      if (successMessage) toast.success(successMessage);
    } catch (error) {
      console.error(errorMessage || 'Failed to save:', error);
      toast.error(error?.message || errorMessage || 'Failed to save');
    } finally {
      setIsSaving(false);
    }
  };

  return { isEditing, editedValue, setEditedValue, isSaving, startEdit, save, cancel };
}
