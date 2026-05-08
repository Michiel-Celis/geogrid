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

export type GeoJsonPolygon = {
  type: 'Polygon';
  coordinates: number[][][];
};

export type MainPlot = {
  id: string;
  projectId: string;
  geometry: GeoJsonPolygon;
  areaSqM: number;
  createdAt: string;
  updatedAt: string;
};

export const mainPlots = {
  get: (projectId: string) => api<MainPlot | undefined>(`/api/projects/${projectId}/main-plot`),
  put: (projectId: string, geometry: GeoJsonPolygon) =>
    api<MainPlot>(`/api/projects/${projectId}/main-plot`, {
      method: 'PUT',
      body: JSON.stringify({ geometry }),
    }),
  remove: (projectId: string) =>
    api<void>(`/api/projects/${projectId}/main-plot`, { method: 'DELETE' }),
};

export type GeoJsonLineString = {
  type: 'LineString';
  coordinates: number[][];
};

export type RoadClass = 'arterial' | 'collector' | 'local' | 'alley';

export type Road = {
  id: string;
  projectId: string;
  name?: string | null;
  class: RoadClass;
  lanes: number;
  widthMeters: number;
  hasFootpath: boolean;
  hasBikepath: boolean;
  geometry: GeoJsonLineString;
  createdAt: string;
  updatedAt: string;
};

export type RoadInput = Omit<Road, 'id' | 'projectId' | 'createdAt' | 'updatedAt'>;

export const roads = {
  list: (projectId: string) => api<Road[]>(`/api/projects/${projectId}/roads`),
  create: (projectId: string, input: RoadInput) =>
    api<Road>(`/api/projects/${projectId}/roads`, { method: 'POST', body: JSON.stringify(input) }),
  update: (projectId: string, id: string, input: RoadInput) =>
    api<Road>(`/api/projects/${projectId}/roads/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  remove: (projectId: string, id: string) =>
    api<void>(`/api/projects/${projectId}/roads/${id}`, { method: 'DELETE' }),
};

export type ReservedKind = 'town_square' | 'forest' | 'park' | 'pond';

export type ReservedArea = {
  id: string;
  projectId: string;
  name?: string | null;
  kind: ReservedKind;
  geometry: GeoJsonPolygon;
  createdAt: string;
  updatedAt: string;
};

export type ReservedAreaInput = Omit<ReservedArea, 'id' | 'projectId' | 'createdAt' | 'updatedAt'>;

export const reservedAreas = {
  list: (projectId: string) => api<ReservedArea[]>(`/api/projects/${projectId}/reserved-areas`),
  create: (projectId: string, input: ReservedAreaInput) =>
    api<ReservedArea>(`/api/projects/${projectId}/reserved-areas`, { method: 'POST', body: JSON.stringify(input) }),
  update: (projectId: string, id: string, input: ReservedAreaInput) =>
    api<ReservedArea>(`/api/projects/${projectId}/reserved-areas/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  remove: (projectId: string, id: string) =>
    api<void>(`/api/projects/${projectId}/reserved-areas/${id}`, { method: 'DELETE' }),
};

export type SuggestiveLine = {
  id: string;
  projectId: string;
  name?: string | null;
  weight: number;
  toleranceMeters: number;
  geometry: GeoJsonLineString;
  createdAt: string;
  updatedAt: string;
};

export type SuggestiveLineInput = Omit<SuggestiveLine, 'id' | 'projectId' | 'createdAt' | 'updatedAt'>;

export const suggestiveLines = {
  list: (projectId: string) => api<SuggestiveLine[]>(`/api/projects/${projectId}/suggestive-lines`),
  create: (projectId: string, input: SuggestiveLineInput) =>
    api<SuggestiveLine>(`/api/projects/${projectId}/suggestive-lines`, { method: 'POST', body: JSON.stringify(input) }),
  update: (projectId: string, id: string, input: SuggestiveLineInput) =>
    api<SuggestiveLine>(`/api/projects/${projectId}/suggestive-lines/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  remove: (projectId: string, id: string) =>
    api<void>(`/api/projects/${projectId}/suggestive-lines/${id}`, { method: 'DELETE' }),
};

/** Suggest a UTM EPSG SRID for the given lat/lon (WGS84 northern/southern hemisphere). */
export function suggestUtmSrid(lat: number, lon: number): number {
  const zone = Math.floor((lon + 180) / 6) + 1;
  return lat >= 0 ? 32600 + zone : 32700 + zone;
}
