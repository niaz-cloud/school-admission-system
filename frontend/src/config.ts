declare global {
  interface Window {
    __APP_CONFIG__?: { API_BASE_URL?: string };
  }
}

/**
 * Backend base URL. Resolution order:
 * 1. window.__APP_CONFIG__ (public/config.js; rewritten from the API_BASE_URL env var when the
 *    Docker container starts, so the image does not need a rebuild to point elsewhere)
 * 2. VITE_API_BASE_URL (build-time, for local development)
 * 3. http://localhost:8080
 */
export function apiBaseUrl(): string {
  const fromRuntime = typeof window !== 'undefined' ? window.__APP_CONFIG__?.API_BASE_URL : undefined;
  const url = fromRuntime || import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080';
  return url.replace(/\/+$/, '');
}
