import axios from 'axios';
import { validateDocumentArray } from './Validation';
import { API_BASE_URL, parseApiError } from './apiClient';

export async function searchDocuments(query) {
  if (!query?.trim()) {
    throw new Error('Search query is required');
  }

  try {
    const res = await axios.get(`${API_BASE_URL}/documents/search`, {
      params: { query },
    });
    return validateDocumentArray(res.data);
  } catch (error) {
    throw parseApiError(error);
  }
}
