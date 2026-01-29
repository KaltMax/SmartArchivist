import axios from 'axios';
import { validateId, validateName, validateSummary, validateDocumentDto } from './Validation';

const API_BASE_URL = '/api';

// PATCH /api/documents/{id} -> DocumentDto
export async function updateDocument(id, name, summary) {
  validateId(id);

  // Validate at least one field is provided
  if (!name && !summary) {
    throw new Error('At least one field (name or summary) must be provided for update');
  }

  // Validate fields if provided
  if (name !== null && name !== undefined) {
    validateName(name);
  }
  if (summary !== null && summary !== undefined) {
    validateSummary(summary);
  }

  try {
    const payload = {};
    if (name !== null && name !== undefined) payload.name = name;
    if (summary !== null && summary !== undefined) payload.summary = summary;

    const res = await axios.patch(`${API_BASE_URL}/documents/${id}`, payload);
    return validateDocumentDto(res.data);
  } catch (error) {
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
}
