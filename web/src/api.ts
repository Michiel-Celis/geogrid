export const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

const TOKEN_KEY = 'geogrid.token';
const USER_KEY = 'geogrid.user';

export type AuthUser = { userId: string; email: string };

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function getUser(): AuthUser | null {
  const raw = localStorage.getItem(USER_KEY);
  return raw ? (JSON.parse(raw) as AuthUser) : null;
}

export function setSession(token: string, user: AuthUser) {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

export class ApiError extends Error {
  status: number;
  body?: unknown;
  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Content-Type', 'application/json');
  const token = getToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers });
  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const body: unknown = text ? JSON.parse(text) : undefined;
  if (!res.ok) {
    const message =
      (body && typeof body === 'object' && 'title' in body && typeof (body as { title: unknown }).title === 'string')
        ? (body as { title: string }).title
        : `HTTP ${res.status}`;
    throw new ApiError(res.status, message, body);
  }
  return body as T;
}

export type AuthResponse = {
  token: string;
  expiresAt: string;
  userId: string;
  email: string;
};

export type Project = {
  id: string;
  name: string;
  description?: string | null;
  srid: number;
  centerLat: number;
  centerLon: number;
  createdAt: string;
  updatedAt: string;
};

export type CreateProjectInput = Omit<Project, 'id' | 'createdAt' | 'updatedAt'>;

export const auth = {
  register: (email: string, password: string) =>
    api<AuthResponse>('/api/auth/register', { method: 'POST', body: JSON.stringify({ email, password }) }),
  login: (email: string, password: string) =>
    api<AuthResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
};

export const projects = {
  list: () => api<Project[]>('/api/projects'),
  get: (id: string) => api<Project>(`/api/projects/${id}`),
  create: (input: CreateProjectInput) =>
    api<Project>('/api/projects', { method: 'POST', body: JSON.stringify(input) }),
  update: (id: string, input: CreateProjectInput) =>
    api<Project>(`/api/projects/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  remove: (id: string) => api<void>(`/api/projects/${id}`, { method: 'DELETE' }),
};

/** Suggest a UTM EPSG SRID for the given lat/lon (WGS84 northern/southern hemisphere). */
export function suggestUtmSrid(lat: number, lon: number): number {
  const zone = Math.floor((lon + 180) / 6) + 1;
  return lat >= 0 ? 32600 + zone : 32700 + zone;
}
