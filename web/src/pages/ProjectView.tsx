import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import Map, { Marker } from 'react-map-gl/maplibre';
import 'maplibre-gl/dist/maplibre-gl.css';
import { projects, type Project } from '../api';

const OSM_STYLE = {
  version: 8,
  sources: {
    osm: {
      type: 'raster',
      tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
      tileSize: 256,
      attribution: '© OpenStreetMap contributors',
    },
  },
  layers: [{ id: 'osm', type: 'raster', source: 'osm' }],
} as const;

export default function ProjectView() {
  const { id } = useParams<{ id: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    projects.get(id).then(setProject).catch((e: unknown) =>
      setError(e instanceof Error ? e.message : String(e))
    );
  }, [id]);

  if (error) return <p style={{ color: 'crimson', padding: '2rem' }}>{error}</p>;
  if (!project) return <p style={{ padding: '2rem' }}>Loading…</p>;

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', padding: '2rem', maxWidth: 1200, margin: '0 auto' }}>
      <p><Link to="/projects">← All projects</Link></p>
      <h1>{project.name}</h1>
      {project.description && <p>{project.description}</p>}
      <p style={{ color: '#666' }}>
        SRID {project.srid} · center {project.centerLat.toFixed(5)}, {project.centerLon.toFixed(5)}
      </p>

      <div style={{ height: 600, border: '1px solid #ddd', borderRadius: 6, overflow: 'hidden' }}>
        <Map
          initialViewState={{ longitude: project.centerLon, latitude: project.centerLat, zoom: 14 }}
          mapStyle={OSM_STYLE as never}
          style={{ width: '100%', height: '100%' }}
        >
          <Marker longitude={project.centerLon} latitude={project.centerLat} color="#d33" />
        </Map>
      </div>

      <p style={{ marginTop: '1rem', color: '#666' }}>
        Drawing tools (M2) coming next: draw the main plot polygon here.
      </p>
    </div>
  );
}
