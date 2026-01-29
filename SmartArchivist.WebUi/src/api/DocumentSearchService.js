import axios from 'axios';
import { validateDocumentArray } from './Validation';

const API_BASE_URL = '/api';

export const searchDocuments = async (query) => {
  if (!query?.trim()) {
    throw new Error('Search query is required');
  }

  try {
    const res = await axios.get(`${API_BASE_URL}/documents/search`, {
      params: { query }
    });

    return validateDocumentArray(res.data);
  } catch (error) {
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
};