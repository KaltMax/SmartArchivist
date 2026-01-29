import axios from 'axios';
import { validateId } from './Validation';

const API_BASE_URL = '/api';

function parseFilenameFromContentDisposition(disposition) {
  if (!disposition) return null;

  const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition);
  const filenamePart = match?.[1];
  if (!filenamePart) return null;

  const unquoted = filenamePart.replace(/(^"+)|("+$)/g, '');
  try {
    return decodeURIComponent(unquoted);
  } catch {
    return unquoted;
  }
}

// direct download of a document by its ID
export async function downloadDocumentById(id, fallbackName = 'document') {
  validateId(id);

  try {
    const res = await axios.get(`${API_BASE_URL}/documents/${id}/download`, {
      responseType: 'blob',
    });

    const contentDisposition = res.headers?.['content-disposition'];
    const inferred = parseFilenameFromContentDisposition(contentDisposition);
    const filename = inferred || fallbackName;

    const blob = new Blob([res.data], {
      type: res.headers?.['content-type'] || 'application/octet-stream',
    });

    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);
  } catch (error) {
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
}

// for viewer: create a Blob URL (uses axios auth header)
export async function createPdfBlobUrl(id) {
  validateId(id);
  try {
    const res = await axios.get(`${API_BASE_URL}/documents/${id}/download`, {
      responseType: 'blob',
    });
    const type = res.headers?.['content-type'] || 'application/pdf';
    const blob = new Blob([res.data], { type });
    return window.URL.createObjectURL(blob);
  } catch (error) {
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
}