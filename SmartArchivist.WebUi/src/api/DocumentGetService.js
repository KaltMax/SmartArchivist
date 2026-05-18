import axios from 'axios';
import { validateDocumentArray } from './Validation';
import { API_BASE_URL, parseApiError } from './ApiClient';

// GET /api/documents -> IEnumerable<DocumentDto>
export async function getAllDocuments() {
  try {
    const res = await axios.get(`${API_BASE_URL}/documents`);
    return validateDocumentArray(res.data);
  } catch (error) {
    throw parseApiError(error);
  }
}
