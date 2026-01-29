import axios from 'axios';
import { validateDocumentArray } from './Validation';

const API_BASE_URL = '/api';

// GET /api/documents -> IEnumerable<DocumentDto>
export async function getAllDocuments() {
  try {
    const res = await axios.get(`${API_BASE_URL}/documents`);
    return validateDocumentArray(res.data);
  } catch (error) {
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
}