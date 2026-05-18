import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// react-toastify is invoked from inside AuthService on fetch errors;
// stub it so tests don't try to render toasts.
vi.mock('react-toastify', () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

const axios = (await import('axios')).default;

// Capture installed interceptors so we can invoke the response-error handler directly.
const installed = { request: [], response: [] };
const origRequestUse = axios.interceptors.request.use.bind(axios.interceptors.request);
const origResponseUse = axios.interceptors.response.use.bind(axios.interceptors.response);
axios.interceptors.request.use = (fulfilled, rejected) => {
  installed.request.push({ fulfilled, rejected });
  return origRequestUse(fulfilled, rejected);
};
axios.interceptors.response.use = (fulfilled, rejected) => {
  installed.response.push({ fulfilled, rejected });
  return origResponseUse(fulfilled, rejected);
};

// Importing AuthService registers the interceptors on the axios instance above.
const { clearToken } = await import('../src/api/AuthService');

const responseErrorHandler = installed.response[0].rejected;

// Build a stub adapter response (matches the shape axios expects).
function adapterResponse(config, data) {
  return { data, status: 200, statusText: 'OK', headers: {}, config, request: {} };
}

const TOKEN_URL = '/api/auth/token';

describe('AuthService response interceptor', () => {
  let originalAdapter;

  beforeEach(() => {
    clearToken();
    localStorage.clear();
    originalAdapter = axios.defaults.adapter;
  });

  afterEach(() => {
    axios.defaults.adapter = originalAdapter;
  });

  it('rethrows non-401 errors unchanged', async () => {
    const error = {
      response: { status: 500 },
      config: { url: '/api/documents' },
    };
    await expect(responseErrorHandler(error)).rejects.toBe(error);
  });

  it('rethrows 401 from the token endpoint without refreshing', async () => {
    const error = {
      response: { status: 401 },
      config: { url: TOKEN_URL },
    };
    await expect(responseErrorHandler(error)).rejects.toBe(error);
  });

  it('rethrows when a request has already been retried once', async () => {
    const error = {
      response: { status: 401 },
      config: { url: '/api/documents', _retry: true },
    };
    await expect(responseErrorHandler(error)).rejects.toBe(error);
  });

  it('on 401, refreshes the token and retries the original request', async () => {
    localStorage.setItem('jwt_token', 'stale-token');

    const adapter = vi.fn((config) => {
      if (config.url === TOKEN_URL) {
        return Promise.resolve(adapterResponse(config, { token: 'fresh-token' }));
      }
      return Promise.resolve(adapterResponse(config, 'retried-ok'));
    });
    axios.defaults.adapter = adapter;

    const original = { url: '/api/documents', headers: {}, method: 'get' };
    const error = { response: { status: 401 }, config: original };

    const result = await responseErrorHandler(error);

    // Adapter called twice: once for the token refresh, once for the retried request.
    expect(adapter).toHaveBeenCalledTimes(2);
    expect(adapter.mock.calls[0][0].url).toBe(TOKEN_URL);
    expect(adapter.mock.calls[1][0].url).toBe('/api/documents');

    expect(original._retry).toBe(true);
    expect(localStorage.getItem('jwt_token')).toBe('fresh-token');
    expect(original.headers.Authorization).toBe('Bearer fresh-token');
    expect(result.data).toBe('retried-ok');
  });

  it('shares a single refresh across concurrent 401s', async () => {
    localStorage.setItem('jwt_token', 'stale-token');

    let releaseToken;
    const adapter = vi.fn((config) => {
      if (config.url === TOKEN_URL) {
        return new Promise((resolve) => {
          releaseToken = () => resolve(adapterResponse(config, { token: 'fresh-token' }));
        });
      }
      return Promise.resolve(adapterResponse(config, 'retried-ok'));
    });
    axios.defaults.adapter = adapter;

    const errA = {
      response: { status: 401 },
      config: { url: '/api/documents', headers: {}, method: 'get' },
    };
    const errB = {
      response: { status: 401 },
      config: { url: '/api/documents/123', headers: {}, method: 'get' },
    };

    const pA = responseErrorHandler(errA);
    const pB = responseErrorHandler(errB);

    // Flush microtasks so both 401 handlers reach their await refreshPromise.
    await Promise.resolve();
    await Promise.resolve();

    const tokenCalls = adapter.mock.calls.filter((c) => c[0].url === TOKEN_URL);
    expect(tokenCalls).toHaveLength(1);

    releaseToken();
    await Promise.all([pA, pB]);

    // Still exactly one refresh — both 401s shared it.
    expect(adapter.mock.calls.filter((c) => c[0].url === TOKEN_URL)).toHaveLength(1);
  });
});
