import buffer from '@turf/buffer';
import type { Feature, LineString, Polygon } from 'geojson';
import type { GeoJsonLineString, GeoJsonPolygon, ReservedKind, RoadClass } from './api';

export type LonLat = [number, number];

export function ringFromVertices(verts: LonLat[]): GeoJsonPolygon {
  const ring = [...verts, verts[0]].map((v) => [v[0], v[1]]);
  return { type: 'Polygon', coordinates: [ring] };
}

export function verticesFromPolygon(p: GeoJsonPolygon): LonLat[] {
  const ring = p.coordinates[0];
  const open = ring.slice(0, ring.length - 1);
  return open.map((c) => [c[0], c[1]] as LonLat);
}

export function lineFromVertices(verts: LonLat[]): GeoJsonLineString {
  return { type: 'LineString', coordinates: verts.map((v) => [v[0], v[1]]) };
}

export function verticesFromLine(l: GeoJsonLineString): LonLat[] {
  return l.coordinates.map((c) => [c[0], c[1]] as LonLat);
}

export function formatArea(sqM: number): string {
  if (sqM >= 10_000) return `${(sqM / 10_000).toFixed(2)} ha (${sqM.toFixed(0)} m²)`;
  return `${sqM.toFixed(1)} m²`;
}

/** Produce a buffered Polygon Feature for a road, width in meters → halfWidth on each side. */
export function bufferRoad(line: GeoJsonLineString, widthMeters: number): Feature<Polygon | null> {
  const lineFeature: Feature<LineString> = {
    type: 'Feature',
    properties: {},
    geometry: line,
  };
  // turf buffer in kilometers
  const buf = buffer(lineFeature, Math.max(widthMeters / 2, 0.5) / 1000, { units: 'kilometers' });
  return (buf as Feature<Polygon | null>) ?? { type: 'Feature', properties: {}, geometry: null };
}

export const ROAD_CLASSES: RoadClass[] = ['arterial', 'collector', 'local', 'alley'];
export const RESERVED_KINDS: ReservedKind[] = ['town_square', 'forest', 'park', 'pond'];

export const ROAD_COLOR: Record<RoadClass, string> = {
  arterial: '#444',
  collector: '#666',
  local: '#888',
  alley: '#aaa',
};

export const RESERVED_COLOR: Record<ReservedKind, { fill: string; outline: string }> = {
  town_square: { fill: '#caa472', outline: '#7c5a2a' },
  forest: { fill: '#2f8a4d', outline: '#1c5630' },
  park: { fill: '#7ec07a', outline: '#3b7a37' },
  pond: { fill: '#4a8fb6', outline: '#235a82' },
};

export function defaultRoadWidthMeters(cls: RoadClass): number {
  switch (cls) {
    case 'arterial': return 14;
    case 'collector': return 10;
    case 'local': return 6;
    case 'alley': return 3.5;
  }
}

export function defaultRoadLanes(cls: RoadClass): number {
  switch (cls) {
    case 'arterial': return 4;
    case 'collector': return 2;
    case 'local': return 2;
    case 'alley': return 1;
  }
}
