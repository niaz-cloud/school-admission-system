import { apiBaseUrl } from '../config';
import type { ProblemBody } from './types';

export const REQUEST_TIMEOUT_MS = 10_000;

/** Every failed call becomes an ApiError with a machine-readable code the UI can translate. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly body: ProblemBody = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }

  /** Extra fields from the error body, e.g. holdId on ALREADY_APPLIED. */
  extra(key: string): unknown {
    return this.body[key];
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  studentId?: string | null;
}

export async function request<T>(path: string, { method = 'GET', body, studentId }: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (studentId) headers['X-Student-Id'] = studentId;

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl()}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: controller.signal,
    });
  } catch {
    // Server down, DNS/CORS failure, or timeout: fetch rejects without a response.
    throw new ApiError(0, 'NETWORK_ERROR', 'Could not reach the server.');
  } finally {
    clearTimeout(timer);
  }

  if (response.status === 204) return undefined as T;

  const text = await response.text();
  let data: unknown = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  if (!response.ok) {
    const problem = (data && typeof data === 'object' ? data : {}) as ProblemBody;
    const fallbackCode = response.status >= 500 ? 'INTERNAL_ERROR' : 'UNKNOWN_ERROR';
    throw new ApiError(
      response.status,
      typeof problem.code === 'string' ? problem.code : fallbackCode,
      typeof problem.detail === 'string' ? problem.detail : response.statusText,
      problem,
    );
  }

  return data as T;
}
