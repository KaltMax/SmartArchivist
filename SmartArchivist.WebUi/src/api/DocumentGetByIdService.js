import axios from 'axios';
import { validateId, validateDocumentDto } from './Validation';
import { API_BASE_URL, parseApiError } from './ApiClient';

// GET /api/documents/{id} -> DocumentDto
export async function getDocumentById(id) {
  validateId(id);
  try {
    const res = await axios.get(`${API_BASE_URL}/documents/${id}`);
    return validateDocumentDto(res.data);
  } catch (error) {
    throw parseApiError(error);
  }
}
