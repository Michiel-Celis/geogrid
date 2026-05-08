import { useState, type FormEvent } from 'react';
import Map, { Marker, type MapMouseEvent } from 'react-map-gl/maplibre';
import { useNavigate } from 'react-router-dom';
import 'maplibre-gl/dist/maplibre-gl.css';
import { projects, suggestUtmSrid } from '../api';

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

export default function NewProject() {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [center, setCenter] = useState<{ lat: number; lon: number } | null>(null);
  const [srid, setSrid] = useState(4326);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function onMapClick(e: MapMouseEvent) {
    const { lat, lng } = e.lngLat;
    setCenter({ lat, lon: lng });
    setSrid(suggestUtmSrid(lat, lng));
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!center) {
      setError('Click the map to set the project location.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const created = await projects.create({
        name,
        description: description || null,
        srid,
        centerLat: center.lat,
        centerLon: center.lon,
      });
      navigate(`/projects/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', padding: '2rem', maxWidth: 1100, margin: '0 auto' }}>
      <h1>New project</h1>
      <form onSubmit={onSubmit} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <label>
            Name
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
          </label>
          <label>
            Description (optional)
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} maxLength={2000} />
          </label>
          <div>
            <strong>Click the map</strong> to set the project location.
          </div>
          {center && (
            <div>
              <div>Center: {center.lat.toFixed(5)}, {center.lon.toFixed(5)}</div>
              <label>
                SRID (suggested UTM zone)
                <input type="number" value={srid} onChange={(e) => setSrid(Number(e.target.value))} required />
              </label>
            </div>
          )}
          {error && <div style={{ color: 'crimson' }}>{error}</div>}
          <button type="submit" disabled={busy || !center}>{busy ? 'Creating…' : 'Create project'}</button>
        </div>

        <div style={{ height: 480, border: '1px solid #ddd', borderRadius: 6, overflow: 'hidden' }}>
          <Map
            initialViewState={{ longitude: 4.4, latitude: 51.2, zoom: 5 }}
            mapStyle={OSM_STYLE as never}
            onClick={onMapClick}
            style={{ width: '100%', height: '100%' }}
          >
            {center && <Marker longitude={center.lon} latitude={center.lat} color="#d33" />}
          </Map>
        </div>
      </form>
    </div>
  );
}
