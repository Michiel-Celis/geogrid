import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import Map, {
  Layer,
  Marker,
  Source,
  type MapMouseEvent,
  type MapRef,
} from 'react-map-gl/maplibre';
import 'maplibre-gl/dist/maplibre-gl.css';
import area from '@turf/area';
import { mainPlots, projects, type GeoJsonPolygon, type MainPlot, type Project } from '../api';

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

type Mode = 'view' | 'draw' | 'edit';
type LonLat = [number, number];

function ringFromVertices(verts: LonLat[]): GeoJsonPolygon {
  const ring = [...verts, verts[0]].map((v) => [v[0], v[1]]);
  return { type: 'Polygon', coordinates: [ring] };
}

function verticesFromPolygon(p: GeoJsonPolygon): LonLat[] {
  const ring = p.coordinates[0];
  const open = ring.slice(0, ring.length - 1);
  return open.map((c) => [c[0], c[1]] as LonLat);
}

function formatArea(sqM: number): string {
  if (sqM >= 10_000) return `${(sqM / 10_000).toFixed(2)} ha (${sqM.toFixed(0)} m²)`;
  return `${sqM.toFixed(1)} m²`;
}

function vertexDot(color: string): React.CSSProperties {
  return {
    width: 12,
    height: 12,
    borderRadius: '50%',
    background: '#fff',
    border: `2px solid ${color}`,
    boxShadow: '0 1px 2px rgba(0,0,0,0.3)',
    cursor: 'grab',
  };
}

type ToolbarProps = {
  mode: Mode;
  hasPlot: boolean;
  saving: boolean;
  canUndo: boolean;
  canRedo: boolean;
  draftCount: number;
  onDraw: () => void;
  onFinish: () => void;
  onCancelDraw: () => void;
  onUndo: () => void;
  onRedo: () => void;
  onEdit: () => void;
  onSaveEdit: () => void;
  onCancelEdit: () => void;
  onDelete: () => void;
};

function Toolbar(p: ToolbarProps) {
  const btn: React.CSSProperties = { padding: '0.4rem 0.8rem' };
  return (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', margin: '1rem 0' }}>
      {p.mode === 'view' && (
        <>
          {!p.hasPlot && <button style={btn} onClick={p.onDraw}>Draw main plot</button>}
          {p.hasPlot && <button style={btn} onClick={p.onDraw}>Redraw</button>}
          {p.hasPlot && <button style={btn} onClick={p.onEdit}>Edit vertices</button>}
          {p.hasPlot && <button style={btn} onClick={p.onDelete}>Delete plot</button>}
        </>
      )}
      {p.mode === 'draw' && (
        <>
          <button style={btn} onClick={p.onFinish} disabled={p.saving || p.draftCount < 3}>
            {p.saving ? 'Saving…' : 'Finish'}
          </button>
          <button style={btn} onClick={p.onUndo} disabled={!p.canUndo}>Undo</button>
          <button style={btn} onClick={p.onRedo} disabled={!p.canRedo}>Redo</button>
          <button style={btn} onClick={p.onCancelDraw}>Cancel</button>
        </>
      )}
      {p.mode === 'edit' && (
        <>
          <button style={btn} onClick={p.onSaveEdit} disabled={p.saving}>
            {p.saving ? 'Saving…' : 'Save'}
          </button>
          <button style={btn} onClick={p.onCancelEdit}>Cancel</button>
        </>
      )}
    </div>
  );
}

export default function ProjectView() {
  const { id } = useParams<{ id: string }>();
  const mapRef = useRef<MapRef | null>(null);

  const [project, setProject] = useState<Project | null>(null);
  const [plot, setPlot] = useState<MainPlot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [mode, setMode] = useState<Mode>('view');
  const [draft, setDraft] = useState<LonLat[]>([]);
  const [history, setHistory] = useState<LonLat[][]>([[]]);
  const [historyIdx, setHistoryIdx] = useState(0);
  const [editVerts, setEditVerts] = useState<LonLat[]>([]);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    Promise.all([projects.get(id), mainPlots.get(id)])
      .then(([p, mp]) => {
        if (cancelled) return;
        setProject(p);
        setPlot(mp ?? null);
      })
      .catch((e: unknown) => {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  const pushHistory = useCallback(
    (next: LonLat[]) => {
      setHistory((h) => {
        const trimmed = h.slice(0, historyIdx + 1);
        trimmed.push(next);
        setHistoryIdx(trimmed.length - 1);
        return trimmed;
      });
      setDraft(next);
    },
    [historyIdx],
  );

  const startDraw = () => {
    setMode('draw');
    setDraft([]);
    setHistory([[]]);
    setHistoryIdx(0);
    setError(null);
    setInfo('Click to add vertices. Double-click or press Enter to finish. Esc cancels.');
  };

  const cancelDraw = useCallback(() => {
    setMode('view');
    setDraft([]);
    setHistory([[]]);
    setHistoryIdx(0);
    setInfo(null);
  }, []);

  const finishDraw = useCallback(async () => {
    if (!id) return;
    if (draft.length < 3) {
      setError('Need at least 3 vertices.');
      return;
    }
    setError(null);
    setSaving(true);
    try {
      const saved = await mainPlots.put(id, ringFromVertices(draft));
      setPlot(saved);
      cancelDraw();
      setInfo('Main plot saved.');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setSaving(false);
    }
  }, [id, draft, cancelDraw]);

  const startEdit = () => {
    if (!plot) return;
    setEditVerts(verticesFromPolygon(plot.geometry));
    setMode('edit');
    setError(null);
    setInfo('Drag vertices. Save to persist, Cancel to discard.');
  };

  const cancelEdit = () => {
    setMode('view');
    setEditVerts([]);
    setInfo(null);
  };

  const saveEdit = async () => {
    if (!id) return;
    if (editVerts.length < 3) {
      setError('Need at least 3 vertices.');
      return;
    }
    setError(null);
    setSaving(true);
    try {
      const saved = await mainPlots.put(id, ringFromVertices(editVerts));
      setPlot(saved);
      cancelEdit();
      setInfo('Main plot updated.');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setSaving(false);
    }
  };

  const deletePlot = async () => {
    if (!id || !plot) return;
    if (!window.confirm('Delete the main plot?')) return;
    try {
      await mainPlots.remove(id);
      setPlot(null);
      setInfo('Main plot deleted.');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const undo = useCallback(() => {
    if (historyIdx <= 0) return;
    const next = historyIdx - 1;
    setHistoryIdx(next);
    setDraft(history[next]);
  }, [history, historyIdx]);

  const redo = useCallback(() => {
    if (historyIdx >= history.length - 1) return;
    const next = historyIdx + 1;
    setHistoryIdx(next);
    setDraft(history[next]);
  }, [history, historyIdx]);

  const popVertex = useCallback(() => {
    if (draft.length === 0) return;
    pushHistory(draft.slice(0, -1));
  }, [draft, pushHistory]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (mode === 'draw') {
        if (e.key === 'Escape') cancelDraw();
        else if (e.key === 'Enter') void finishDraw();
        else if (e.key === 'Backspace') popVertex();
        else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
          if (e.shiftKey) redo();
          else undo();
        } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') redo();
      } else if (mode === 'edit') {
        if (e.key === 'Escape') cancelEdit();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [mode, cancelDraw, finishDraw, popVertex, undo, redo]);

  const onMapClick = (e: MapMouseEvent) => {
    if (mode !== 'draw') return;
    pushHistory([...draft, [e.lngLat.lng, e.lngLat.lat]]);
  };

  const onMapDblClick = (e: MapMouseEvent) => {
    if (mode !== 'draw') return;
    e.preventDefault();
    void finishDraw();
  };

  const displayPolygon = useMemo<GeoJsonPolygon | null>(() => {
    if (mode === 'edit' && editVerts.length >= 3) return ringFromVertices(editVerts);
    if (mode === 'draw' && draft.length >= 3) return ringFromVertices(draft);
    if (mode === 'view' && plot) return plot.geometry;
    return null;
  }, [mode, editVerts, draft, plot]);

  const draftLine = useMemo(() => {
    if (mode !== 'draw' || draft.length < 2) return null;
    return {
      type: 'Feature' as const,
      properties: {},
      geometry: { type: 'LineString' as const, coordinates: draft },
    };
  }, [mode, draft]);

  const polygonFeature = useMemo(() => {
    if (!displayPolygon) return null;
    return {
      type: 'Feature' as const,
      properties: {},
      geometry: displayPolygon,
    };
  }, [displayPolygon]);

  const liveAreaSqM = useMemo(() => {
    if (!polygonFeature) return null;
    try {
      return area(polygonFeature);
    } catch {
      return null;
    }
  }, [polygonFeature]);

  if (error && !project) return <p style={{ color: 'crimson', padding: '2rem' }}>{error}</p>;
  if (!project) return <p style={{ padding: '2rem' }}>Loading…</p>;

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', padding: '2rem', maxWidth: 1200, margin: '0 auto' }}>
      <p><Link to="/projects">← All projects</Link></p>
      <h1>{project.name}</h1>
      {project.description && <p>{project.description}</p>}
      <p style={{ color: '#666' }}>
        SRID {project.srid} · center {project.centerLat.toFixed(5)}, {project.centerLon.toFixed(5)}
      </p>

      <Toolbar
        mode={mode}
        hasPlot={!!plot}
        saving={saving}
        canUndo={historyIdx > 0}
        canRedo={historyIdx < history.length - 1}
        draftCount={draft.length}
        onDraw={startDraw}
        onFinish={() => void finishDraw()}
        onCancelDraw={cancelDraw}
        onUndo={undo}
        onRedo={redo}
        onEdit={startEdit}
        onSaveEdit={() => void saveEdit()}
        onCancelEdit={cancelEdit}
        onDelete={() => void deletePlot()}
      />

      {error && <p style={{ color: 'crimson' }}>{error}</p>}
      {info && !error && <p style={{ color: '#0a7' }}>{info}</p>}

      <div style={{ display: 'flex', gap: 16, alignItems: 'center', margin: '0.5rem 0', color: '#444' }}>
        {plot && mode === 'view' && <span>Saved area: <strong>{formatArea(plot.areaSqM)}</strong></span>}
        {liveAreaSqM !== null && mode !== 'view' && (
          <span>Live area: <strong>{formatArea(liveAreaSqM)}</strong></span>
        )}
        {mode === 'draw' && <span>{draft.length} vertices</span>}
      </div>

      <div style={{ height: 600, border: '1px solid #ddd', borderRadius: 6, overflow: 'hidden' }}>
        <Map
          ref={(r) => { mapRef.current = r; }}
          initialViewState={{ longitude: project.centerLon, latitude: project.centerLat, zoom: 14 }}
          mapStyle={OSM_STYLE as never}
          style={{ width: '100%', height: '100%' }}
          onClick={onMapClick}
          onDblClick={onMapDblClick}
          doubleClickZoom={mode !== 'draw'}
          cursor={mode === 'draw' ? 'crosshair' : undefined}
        >
          {polygonFeature && (
            <Source id="main-plot" type="geojson" data={polygonFeature}>
              <Layer
                id="main-plot-fill"
                type="fill"
                paint={{ 'fill-color': '#1976d2', 'fill-opacity': 0.25 }}
              />
              <Layer
                id="main-plot-outline"
                type="line"
                paint={{ 'line-color': '#1976d2', 'line-width': 2 }}
              />
            </Source>
          )}
          {draftLine && (
            <Source id="draft-line" type="geojson" data={draftLine}>
              <Layer
                id="draft-line-stroke"
                type="line"
                paint={{ 'line-color': '#d33', 'line-width': 2, 'line-dasharray': [2, 2] }}
              />
            </Source>
          )}

          {mode === 'view' && !plot && (
            <Marker longitude={project.centerLon} latitude={project.centerLat} color="#d33" />
          )}

          {mode === 'draw' && draft.map((v, i) => (
            <Marker key={`d${i}`} longitude={v[0]} latitude={v[1]} anchor="center">
              <div style={vertexDot('#d33')} />
            </Marker>
          ))}

          {mode === 'edit' && editVerts.map((v, i) => (
            <Marker
              key={`e${i}`}
              longitude={v[0]}
              latitude={v[1]}
              anchor="center"
              draggable
              onDrag={(ev) => {
                const next = editVerts.slice();
                next[i] = [ev.lngLat.lng, ev.lngLat.lat];
                setEditVerts(next);
              }}
            >
              <div style={vertexDot('#1976d2')} />
            </Marker>
          ))}
        </Map>
      </div>

      <p style={{ marginTop: '1rem', color: '#888', fontSize: 13 }}>
        Shortcuts: Enter = finish · Esc = cancel · Backspace = remove last vertex · Ctrl+Z / Ctrl+Shift+Z = undo / redo.
      </p>
    </div>
  );
}
