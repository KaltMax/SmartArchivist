import axios from 'axios';
import { validateId, validateDocumentDto } from './Validation';

const API_BASE_URL = '/api';

// GET /api/documents/{id} -> DocumentDto
export async function getDocumentById(id) {
  validateId(id);
  try {
    const res = await axios.get(`${API_BASE_URL}/documents/${id}`);
    return validateDocumentDto(res.data);
  } catch (error) {
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
}