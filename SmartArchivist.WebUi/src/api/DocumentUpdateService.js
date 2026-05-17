import axios from 'axios';
import { validateId, validateName, validateSummary, validateDocumentDto } from './Validation';
import { API_BASE_URL, parseApiError } from './apiClient';

// PATCH /api/documents/{id} -> DocumentDto
export async function updateDocument(id, name, summary) {
  validateId(id);

  if (name == null && summary == null) {
    throw new Error('At least one field (name or summary) must be provided for update');
  }

  if (name != null) validateName(name);
  if (summary != null) validateSummary(summary);

  try {
    const payload = {};
    if (name != null) payload.name = name;
    if (summary != null) payload.summary = summary;

    const res = await axios.patch(`${API_BASE_URL}/documents/${id}`, payload);
    return validateDocumentDto(res.data);
  } catch (error) {
    throw parseApiError(error);
  }
}
