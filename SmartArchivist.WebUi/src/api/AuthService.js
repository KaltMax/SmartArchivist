import axios from 'axios';
import { toast } from 'react-toastify';

const API_BASE_URL = '/api';

let token = null;

// Add Authorization header to all requests except for the token fetch request
// Interceptor = function that is called before a request is sent or after a response is received
axios.interceptors.request.use(
  (config) => {
    const token = getToken();
    if (token && config.url !== `${API_BASE_URL}/auth/token`) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
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
