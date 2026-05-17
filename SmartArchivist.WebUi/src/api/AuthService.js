import axios from 'axios';
import { toast } from 'react-toastify';

const API_BASE_URL = '/api';
const TOKEN_URL = `${API_BASE_URL}/auth/token`;

let token = null;
let refreshPromise = null;

// Add Authorization header to all requests except for the token fetch request
// Interceptor = function that is called before a request is sent or after a response is received
axios.interceptors.request.use(
  (config) => {
    const token = getToken();
    if (token && config.url !== TOKEN_URL) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// On 401, refresh the token once and retry the original request.
// Concurrent 401s share a single refresh via refreshPromise.
axios.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config;
    const status = error.response?.status;

    if (status !== 401 || !original || original._retry || original.url === TOKEN_URL) {
      throw error;
    }

    original._retry = true;
    if (!refreshPromise) {
      refreshPromise = refreshToken().finally(() => {
        refreshPromise = null;
      });
    }
    const newToken = await refreshPromise;
    original.headers.Authorization = `Bearer ${newToken}`;
    return axios(original);
  }
);

export const fetchToken = async () => {
  try {
    const response = await axios.post(`${API_BASE_URL}/auth/token`);
    token = response.data.token;
    localStorage.setItem('jwt_token', token);
    console.log('JWT token fetched successfully');
    return token;
  } catch (error) {
    const errorMessage = error.response?.data?.error || error.message;
    console.error('Error fetching token:', errorMessage);
    toast.error(`Authentication failed: ${errorMessage}`);
    throw new Error(errorMessage);
  }
};

export const getToken = () => {
  if (token) return token;
  token = localStorage.getItem('jwt_token');
  return token;
};

export const clearToken = () => {
  token = null;
  localStorage.removeItem('jwt_token');
  console.log('JWT token cleared');
};

export const hasToken = () => Boolean(getToken());

export const initializeAuth = async () => {
  const existingToken = localStorage.getItem('jwt_token');

  if (existingToken) {
    token = existingToken;
    console.log('Using existing JWT token');
    return token;
  }

  console.log('No existing token found, fetching new JWT token...');
  return await fetchToken();
};

export const refreshToken = async () => {
  console.log('Refreshing JWT token...');
  clearToken();
  return await fetchToken();
};
