import { fetch } from 'expo/fetch';
import { getValidAccessToken, refreshAccessToken } from '@/lib/auth/token-provider';
import { apiBaseUrl } from '@/lib/host';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, init?: RequestInit, allowRetry = true): Promise<T> {
  const token = await getValidAccessToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string> | undefined),
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl()}${path}`, {
      ...init,
      headers,
    });
  } catch {
    throw new ApiError(0, 'network_error', 'Network request failed');
  }

  // The token looked live when attached yet the gateway disagrees (it died in flight, or the device
  // clock is skewed): force one refresh and replay. `allowRetry` makes the replay terminal, so a
  // genuinely unauthorized call surfaces its 401 instead of looping.
  if (response.status === 401 && allowRetry) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      return request<T>(path, init, false);
    }
  }

  if (!response.ok) {
    let code = 'api_error';
    let message = `HTTP ${response.status}`;
    try {
      const body = (await response.json()) as { code?: string; message?: string };
      code = body.code ?? code;
      message = body.message ?? message;
    } catch {
      // ignore
    }
    throw new ApiError(response.status, code, message);
  }

  // No-content responses (e.g. 204 from fire-and-forget actions) have no body to parse.
  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export const apiClient = {
  get: <T>(path: string, init?: RequestInit): Promise<T> => request<T>(path, init),
  post: <T>(path: string, body: unknown): Promise<T> =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
};
