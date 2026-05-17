import axios from 'axios';
import { validateId } from './Validation';
import { API_BASE_URL, parseApiError } from './apiClient';

export async function deleteDocument(id) {
  validateId(id);
  try {
    await axios.delete(`${API_BASE_URL}/documents/${id}`);
  } catch (error) {
    throw parseApiError(error);
  }
}
