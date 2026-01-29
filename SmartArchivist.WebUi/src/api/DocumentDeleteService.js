import axios from 'axios';
import { validateId } from './Validation';

const API_BASE_URL = '/api';

export async function deleteDocument(id) {
  validateId(id);
  try {
    await axios.delete(`${API_BASE_URL}/documents/${id}`);
  } catch (error) {
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
}