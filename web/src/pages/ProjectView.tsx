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
import {
  generations,
  mainPlots,
  projects,
  reservedAreas,
  roads,
  suggestiveLines,
  DEFAULT_GENERATION_PARAMS,
  type GenerationParams,
  type GenerationRun,
  type MainPlot,
  type Project,
  type ReservedArea,
  type ReservedKind,
  type Road,
  type RoadClass,
  type SuggestiveLine,
} from '../api';
import {
  RESERVED_COLOR,
  RESERVED_KINDS,
  ROAD_CLASSES,
  ROAD_COLOR,
  bufferRoad,
  defaultRoadLanes,
  defaultRoadWidthMeters,
  formatArea,
  lineFromVertices,
  ringFromVertices,
  verticesFromLine,
  verticesFromPolygon,
  type LonLat,
} from '../geo';

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

type Tool =
  | 'select'
  | 'draw-main-plot'
  | 'draw-road'
  | 'draw-reserved'
  | 'draw-suggestive';

type SelectionKind = 'main-plot' | 'road' | 'reserved' | 'suggestive';
type Selection = { kind: SelectionKind; id: string };

type LayersVisible = {
  mainPlot: boolean;
  roads: boolean;
  reserved: boolean;
  suggestive: boolean;
  plots: boolean;
};

const DEFAULT_VISIBLE: LayersVisible = {
  mainPlot: true,
  roads: true,
  reserved: true,
  suggestive: true,
  plots: true,
};

function vertexDot(color: string): React.CSSProperties {
  return {
    width: 12, height: 12, borderRadius: '50%', background: '#fff',
    border: `2px solid ${color}`, boxShadow: '0 1px 2px rgba(0,0,0,0.3)', cursor: 'grab',
  };
}

export default function ProjectView() {
  const { id } = useParams<{ id: string }>();
  const mapRef = useRef<MapRef | null>(null);

  const [project, setProject] = useState<Project | null>(null);
  const [mainPlot, setMainPlot] = useState<MainPlot | null>(null);
  const [roadList, setRoadList] = useState<Road[]>([]);
  const [reservedList, setReservedList] = useState<ReservedArea[]>([]);
  const [suggestiveList, setSuggestiveList] = useState<SuggestiveLine[]>([]);

  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [tool, setTool] = useState<Tool>('select');
  const [draft, setDraft] = useState<LonLat[]>([]);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [editVerts, setEditVerts] = useState<LonLat[] | null>(null); // when set, vertex-edit handles shown
  const [visible, setVisible] = useState<LayersVisible>(DEFAULT_VISIBLE);

  const [genParams, setGenParams] = useState<GenerationParams>(DEFAULT_GENERATION_PARAMS);
  const [activeRun, setActiveRun] = useState<GenerationRun | null>(null);
  const [allRuns, setAllRuns] = useState<GenerationRun[]>([]);

  const isDrawing = tool !== 'select';

  // ----- load -----
  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    Promise.all([
      projects.get(id),
      mainPlots.get(id),
      roads.list(id),
      reservedAreas.list(id),
      suggestiveLines.list(id),
      generations.list(id),
    ])
      .then(async ([p, mp, rs, ra, sl, runs]) => {
        if (cancelled) return;
        setProject(p);
        setMainPlot(mp ?? null);
        setRoadList(rs);
        setReservedList(ra);
        setSuggestiveList(sl);
        setAllRuns(runs);
        // Pick the most relevant run: committed first, otherwise newest preview.
        const pick = runs.find((r) => r.status === 'committed') ?? runs.find((r) => r.status === 'preview');
        if (pick) {
          const full = await generations.get(p.id, pick.id);
          if (!cancelled) setActiveRun(full);
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      });
    return () => { cancelled = true; };
  }, [id]);

  // ----- helpers -----
  const clearTransient = useCallback(() => {
    setDraft([]);
    setEditVerts(null);
  }, []);

  const switchTool = useCallback((t: Tool) => {
    setTool(t);
    setSelection(null);
    clearTransient();
    setError(null);
    setInfo(t === 'select' ? null : drawHint(t));
  }, [clearTransient]);

  // finish drawing (closes polygon for area tools, keeps open for line tools)
  const finishDraft = useCallback(async () => {
    if (!id) return;
    if (tool === 'select') return;

    const isArea = tool === 'draw-main-plot' || tool === 'draw-reserved';
    const minPoints = isArea ? 3 : 2;
    if (draft.length < minPoints) {
      setError(`Need at least ${minPoints} points.`);
      return;
    }
    setError(null);
    setBusy(true);
    try {
      if (tool === 'draw-main-plot') {
        const saved = await mainPlots.put(id, ringFromVertices(draft));
        setMainPlot(saved);
        setInfo('Main plot saved.');
      } else if (tool === 'draw-road') {
        const cls: RoadClass = 'local';
        const saved = await roads.create(id, {
          name: '',
          class: cls,
          lanes: defaultRoadLanes(cls),
          widthMeters: defaultRoadWidthMeters(cls),
          hasFootpath: false,
          hasBikepath: false,
          geometry: lineFromVertices(draft),
        });
        setRoadList((prev) => [...prev, saved]);
        setSelection({ kind: 'road', id: saved.id });
        setInfo('Road created.');
      } else if (tool === 'draw-reserved') {
        const kind: ReservedKind = 'park';
        const saved = await reservedAreas.create(id, {
          name: '',
          kind,
          geometry: ringFromVertices(draft),
        });
        setReservedList((prev) => [...prev, saved]);
        setSelection({ kind: 'reserved', id: saved.id });
        setInfo('Reserved area created.');
      } else if (tool === 'draw-suggestive') {
        const saved = await suggestiveLines.create(id, {
          name: '',
          weight: 1,
          toleranceMeters: 25,
          geometry: lineFromVertices(draft),
        });
        setSuggestiveList((prev) => [...prev, saved]);
        setSelection({ kind: 'suggestive', id: saved.id });
        setInfo('Suggestive line created.');
      }
      setTool('select');
      clearTransient();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }, [id, tool, draft, clearTransient]);

  // ----- keyboard -----
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (isDrawing) {
        if (e.key === 'Escape') switchTool('select');
        else if (e.key === 'Enter') void finishDraft();
        else if (e.key === 'Backspace') setDraft((d) => d.slice(0, -1));
      } else if (e.key === 'Escape') {
        setSelection(null);
        setEditVerts(null);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [isDrawing, switchTool, finishDraft]);

  // ----- map interactions -----
  const SELECTABLE_LAYERS = useMemo(() => [
    'main-plot-fill',
    'road-buffer-fill',
    'reserved-fill',
    'suggestive-line-stroke',
  ], []);

  const onMapClick = (e: MapMouseEvent) => {
    if (isDrawing) {
      setDraft((d) => [...d, [e.lngLat.lng, e.lngLat.lat]]);
      return;
    }
    // Selection mode: query rendered features under cursor.
    const map = mapRef.current?.getMap();
    if (!map) return;
    const feats = map.queryRenderedFeatures(e.point, { layers: SELECTABLE_LAYERS.filter((l) => map.getLayer(l)) });
    if (!feats.length) {
      setSelection(null);
      setEditVerts(null);
      return;
    }
    const f = feats[0];
    const props = f.properties ?? {};
    const fid = (props['id'] as string | undefined) ?? '';
    const kind = (props['kind'] as SelectionKind | undefined);
    if (!kind) return;
    setSelection({ kind, id: fid });
    setEditVerts(null);
  };

  const onMapDblClick = (e: MapMouseEvent) => {
    if (!isDrawing) return;
    e.preventDefault();
    void finishDraft();
  };

  // ----- selection details -----
  const selected = useMemo(() => {
    if (!selection) return null;
    if (selection.kind === 'main-plot') return mainPlot ? { kind: 'main-plot' as const, value: mainPlot } : null;
    if (selection.kind === 'road') {
      const r = roadList.find((x) => x.id === selection.id);
      return r ? { kind: 'road' as const, value: r } : null;
    }
    if (selection.kind === 'reserved') {
      const r = reservedList.find((x) => x.id === selection.id);
      return r ? { kind: 'reserved' as const, value: r } : null;
    }
    if (selection.kind === 'suggestive') {
      const r = suggestiveList.find((x) => x.id === selection.id);
      return r ? { kind: 'suggestive' as const, value: r } : null;
    }
    return null;
  }, [selection, mainPlot, roadList, reservedList, suggestiveList]);

  // ----- start vertex edit -----
  const startVertexEdit = () => {
    if (!selected) return;
    if (selected.kind === 'main-plot') setEditVerts(verticesFromPolygon(selected.value.geometry));
    else if (selected.kind === 'road') setEditVerts(verticesFromLine(selected.value.geometry));
    else if (selected.kind === 'reserved') setEditVerts(verticesFromPolygon(selected.value.geometry));
    else if (selected.kind === 'suggestive') setEditVerts(verticesFromLine(selected.value.geometry));
  };

  const cancelVertexEdit = () => setEditVerts(null);

  const saveVertexEdit = async () => {
    if (!id || !selected || !editVerts) return;
    setBusy(true);
    setError(null);
    try {
      if (selected.kind === 'main-plot') {
        if (editVerts.length < 3) throw new Error('Need ≥3 vertices.');
        const saved = await mainPlots.put(id, ringFromVertices(editVerts));
        setMainPlot(saved);
      } else if (selected.kind === 'road') {
        if (editVerts.length < 2) throw new Error('Need ≥2 points.');
        const saved = await roads.update(id, selected.value.id, {
          name: selected.value.name ?? '',
          class: selected.value.class,
          lanes: selected.value.lanes,
          widthMeters: selected.value.widthMeters,
          hasFootpath: selected.value.hasFootpath,
          hasBikepath: selected.value.hasBikepath,
          geometry: lineFromVertices(editVerts),
        });
        setRoadList((prev) => prev.map((r) => (r.id === saved.id ? saved : r)));
      } else if (selected.kind === 'reserved') {
        if (editVerts.length < 3) throw new Error('Need ≥3 vertices.');
        const saved = await reservedAreas.update(id, selected.value.id, {
          name: selected.value.name ?? '',
          kind: selected.value.kind,
          geometry: ringFromVertices(editVerts),
        });
        setReservedList((prev) => prev.map((r) => (r.id === saved.id ? saved : r)));
      } else if (selected.kind === 'suggestive') {
        if (editVerts.length < 2) throw new Error('Need ≥2 points.');
        const saved = await suggestiveLines.update(id, selected.value.id, {
          name: selected.value.name ?? '',
          weight: selected.value.weight,
          toleranceMeters: selected.value.toleranceMeters,
          geometry: lineFromVertices(editVerts),
        });
        setSuggestiveList((prev) => prev.map((r) => (r.id === saved.id ? saved : r)));
      }
      setEditVerts(null);
      setInfo('Geometry updated.');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  // ----- delete selected -----
  const deleteSelected = async () => {
    if (!id || !selected) return;
    if (!window.confirm('Delete this feature?')) return;
    setBusy(true);
    setError(null);
    try {
      if (selected.kind === 'main-plot') {
        await mainPlots.remove(id);
        setMainPlot(null);
      } else if (selected.kind === 'road') {
        await roads.remove(id, selected.value.id);
        setRoadList((prev) => prev.filter((r) => r.id !== selected.value.id));
      } else if (selected.kind === 'reserved') {
        await reservedAreas.remove(id, selected.value.id);
        setReservedList((prev) => prev.filter((r) => r.id !== selected.value.id));
      } else if (selected.kind === 'suggestive') {
        await suggestiveLines.remove(id, selected.value.id);
        setSuggestiveList((prev) => prev.filter((r) => r.id !== selected.value.id));
      }
      setSelection(null);
      setEditVerts(null);
      setInfo('Deleted.');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  // ----- update properties -----
  const saveRoadProps = async (patch: Partial<Road>) => {
    if (!id || !selected || selected.kind !== 'road') return;
    setBusy(true); setError(null);
    try {
      const v = { ...selected.value, ...patch };
      const saved = await roads.update(id, v.id, {
        name: v.name ?? '',
        class: v.class,
        lanes: v.lanes,
        widthMeters: v.widthMeters,
        hasFootpath: v.hasFootpath,
        hasBikepath: v.hasBikepath,
        geometry: v.geometry,
      });
      setRoadList((prev) => prev.map((r) => (r.id === saved.id ? saved : r)));
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  };

  const saveReservedProps = async (patch: Partial<ReservedArea>) => {
    if (!id || !selected || selected.kind !== 'reserved') return;
    setBusy(true); setError(null);
    try {
      const v = { ...selected.value, ...patch };
      const saved = await reservedAreas.update(id, v.id, {
        name: v.name ?? '', kind: v.kind, geometry: v.geometry,
      });
      setReservedList((prev) => prev.map((r) => (r.id === saved.id ? saved : r)));
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  };

  const saveSuggestiveProps = async (patch: Partial<SuggestiveLine>) => {
    if (!id || !selected || selected.kind !== 'suggestive') return;
    setBusy(true); setError(null);
    try {
      const v = { ...selected.value, ...patch };
      const saved = await suggestiveLines.update(id, v.id, {
        name: v.name ?? '', weight: v.weight, toleranceMeters: v.toleranceMeters, geometry: v.geometry,
      });
      setSuggestiveList((prev) => prev.map((r) => (r.id === saved.id ? saved : r)));
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  };

  // ----- generation -----
  const runGenerate = async () => {
    if (!id) return;
    if (!mainPlot) { setError('Draw a main plot first.'); return; }
    setBusy(true); setError(null); setInfo(null);
    try {
      const run = await generations.generate(id, genParams);
      setActiveRun(run);
      setAllRuns((prev) => [run, ...prev.filter((r) => r.id !== run.id)]);
      setInfo(`Generated ${run.plots.length} plots (${run.stats.plotsValid} valid, ${run.stats.plotsInvalid} flagged).`);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  };

  const commitRun = async () => {
    if (!id || !activeRun) return;
    if (!window.confirm('Commit this generation? Other previews for this project will be discarded.')) return;
    setBusy(true); setError(null);
    try {
      const run = await generations.commit(id, activeRun.id);
      setActiveRun(run);
      const refreshed = await generations.list(id);
      setAllRuns(refreshed);
      setInfo('Generation committed.');
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  };

  const discardRun = async () => {
    if (!id || !activeRun) return;
    if (!window.confirm('Discard this generation?')) return;
    setBusy(true); setError(null);
    try {
      await generations.remove(id, activeRun.id);
      setActiveRun(null);
      const refreshed = await generations.list(id);
      setAllRuns(refreshed);
      setInfo('Generation discarded.');
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  };

  // ----- map sources/features -----
  const mainPlotFC = useMemo(() => {
    const features = [];
    if (visible.mainPlot && mainPlot) {
      features.push({
        type: 'Feature' as const,
        id: 1,
        properties: { kind: 'main-plot', id: mainPlot.id },
        geometry: mainPlot.geometry,
      });
    }
    return { type: 'FeatureCollection' as const, features };
  }, [visible.mainPlot, mainPlot]);

  const roadBufferFC = useMemo(() => {
    if (!visible.roads) return { type: 'FeatureCollection' as const, features: [] };
    const features = roadList.map((r) => {
      const buf = bufferRoad(r.geometry, r.widthMeters);
      return {
        type: 'Feature' as const,
        properties: { kind: 'road', id: r.id, class: r.class },
        geometry: buf.geometry ?? r.geometry,
      };
    });
    return { type: 'FeatureCollection' as const, features };
  }, [visible.roads, roadList]);

  const roadCenterlineFC = useMemo(() => {
    if (!visible.roads) return { type: 'FeatureCollection' as const, features: [] };
    return {
      type: 'FeatureCollection' as const,
      features: roadList.map((r) => ({
        type: 'Feature' as const,
        properties: { id: r.id, class: r.class },
        geometry: r.geometry,
      })),
    };
  }, [visible.roads, roadList]);

  const reservedFC = useMemo(() => {
    if (!visible.reserved) return { type: 'FeatureCollection' as const, features: [] };
    return {
      type: 'FeatureCollection' as const,
      features: reservedList.map((r) => ({
        type: 'Feature' as const,
        properties: { kind: 'reserved', id: r.id, kindName: r.kind },
        geometry: r.geometry,
      })),
    };
  }, [visible.reserved, reservedList]);

  const suggestiveFC = useMemo(() => {
    if (!visible.suggestive) return { type: 'FeatureCollection' as const, features: [] };
    return {
      type: 'FeatureCollection' as const,
      features: suggestiveList.map((s) => ({
        type: 'Feature' as const,
        properties: { kind: 'suggestive', id: s.id },
        geometry: s.geometry,
      })),
    };
  }, [visible.suggestive, suggestiveList]);

  const plotsFC = useMemo(() => {
    if (!visible.plots || !activeRun) return { type: 'FeatureCollection' as const, features: [] };
    return {
      type: 'FeatureCollection' as const,
      features: activeRun.plots.map((pl) => ({
        type: 'Feature' as const,
        properties: { id: pl.id, valid: pl.validationPassed ? 1 : 0 },
        geometry: pl.geometry,
      })),
    };
  }, [visible.plots, activeRun]);

  // Draft preview while drawing.
  const draftPreview = useMemo(() => {
    if (!isDrawing || draft.length === 0) return null;
    const isArea = tool === 'draw-main-plot' || tool === 'draw-reserved';
    if (isArea && draft.length >= 3) {
      return {
        type: 'Feature' as const,
        properties: {},
        geometry: ringFromVertices(draft),
      };
    }
    if (draft.length >= 2) {
      return {
        type: 'Feature' as const,
        properties: {},
        geometry: lineFromVertices(draft),
      };
    }
    return null;
  }, [isDrawing, draft, tool]);

  // Live area while drawing/editing area.
  const liveArea = useMemo(() => {
    if (editVerts && (selected?.kind === 'main-plot' || selected?.kind === 'reserved') && editVerts.length >= 3) {
      try { return area({ type: 'Feature', properties: {}, geometry: ringFromVertices(editVerts) }); } catch { return null; }
    }
    if ((tool === 'draw-main-plot' || tool === 'draw-reserved') && draft.length >= 3) {
      try { return area({ type: 'Feature', properties: {}, geometry: ringFromVertices(draft) }); } catch { return null; }
    }
    return null;
  }, [editVerts, selected, tool, draft]);

  if (error && !project) return <p style={{ color: 'crimson', padding: '2rem' }}>{error}</p>;
  if (!project) return <p style={{ padding: '2rem' }}>Loading…</p>;

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', padding: '1.5rem 2rem', maxWidth: 1400, margin: '0 auto' }}>
      <p><Link to="/projects">← All projects</Link></p>
      <h1 style={{ marginBottom: 4 }}>{project.name}</h1>
      {project.description && <p style={{ marginTop: 0 }}>{project.description}</p>}
      <p style={{ color: '#666', marginTop: 0 }}>
        SRID {project.srid} · center {project.centerLat.toFixed(5)}, {project.centerLon.toFixed(5)}
      </p>

      <Toolbar tool={tool} setTool={switchTool} busy={busy} hasMain={!!mainPlot}
        onFinish={() => void finishDraft()} draftCount={draft.length} />

      {error && <p style={{ color: 'crimson' }}>{error}</p>}
      {info && !error && <p style={{ color: '#0a7' }}>{info}</p>}
      {liveArea !== null && <p style={{ color: '#444' }}>Live area: <strong>{formatArea(liveArea)}</strong></p>}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 16 }}>
        <div style={{ height: 640, border: '1px solid #ddd', borderRadius: 6, overflow: 'hidden' }}>
          <Map
            ref={(r) => { mapRef.current = r; }}
            initialViewState={{ longitude: project.centerLon, latitude: project.centerLat, zoom: 14 }}
            mapStyle={OSM_STYLE as never}
            style={{ width: '100%', height: '100%' }}
            onClick={onMapClick}
            onDblClick={onMapDblClick}
            doubleClickZoom={!isDrawing}
            cursor={isDrawing ? 'crosshair' : undefined}
            interactiveLayerIds={SELECTABLE_LAYERS}
          >
            {/* Reserved areas (under roads) */}
            <Source id="reserved" type="geojson" data={reservedFC}>
              <Layer id="reserved-fill" type="fill" paint={{
                'fill-color': [
                  'match', ['get', 'kindName'],
                  'town_square', RESERVED_COLOR.town_square.fill,
                  'forest', RESERVED_COLOR.forest.fill,
                  'park', RESERVED_COLOR.park.fill,
                  'pond', RESERVED_COLOR.pond.fill,
                  '#999',
                ],
                'fill-opacity': 0.55,
              }} />
              <Layer id="reserved-outline" type="line" paint={{
                'line-color': [
                  'match', ['get', 'kindName'],
                  'town_square', RESERVED_COLOR.town_square.outline,
                  'forest', RESERVED_COLOR.forest.outline,
                  'park', RESERVED_COLOR.park.outline,
                  'pond', RESERVED_COLOR.pond.outline,
                  '#444',
                ],
                'line-width': 1.5,
              }} />
            </Source>

            {/* Main plot */}
            <Source id="main-plot" type="geojson" data={mainPlotFC}>
              <Layer id="main-plot-fill" type="fill" paint={{ 'fill-color': '#1976d2', 'fill-opacity': 0.18 }} />
              <Layer id="main-plot-outline" type="line" paint={{ 'line-color': '#1976d2', 'line-width': 2 }} />
            </Source>

            {/* Road buffers */}
            <Source id="road-buffers" type="geojson" data={roadBufferFC}>
              <Layer id="road-buffer-fill" type="fill" paint={{
                'fill-color': [
                  'match', ['get', 'class'],
                  'arterial', ROAD_COLOR.arterial,
                  'collector', ROAD_COLOR.collector,
                  'local', ROAD_COLOR.local,
                  'alley', ROAD_COLOR.alley,
                  '#888',
                ],
                'fill-opacity': 0.65,
              }} />
            </Source>

            {/* Road centerlines */}
            <Source id="road-centerlines" type="geojson" data={roadCenterlineFC}>
              <Layer id="road-centerline" type="line" paint={{
                'line-color': '#fff',
                'line-width': 1,
                'line-dasharray': [2, 4],
              }} />
            </Source>

            {/* Suggestive lines */}
            <Source id="suggestive" type="geojson" data={suggestiveFC}>
              <Layer id="suggestive-line-stroke" type="line" paint={{
                'line-color': '#e6a700', 'line-width': 3, 'line-dasharray': [1, 2],
              }} />
            </Source>

            {/* Generated plots */}
            <Source id="plots" type="geojson" data={plotsFC}>
              <Layer id="plots-fill" type="fill" paint={{
                'fill-color': ['case', ['==', ['get', 'valid'], 1], '#43a047', '#e53935'],
                'fill-opacity': 0.35,
              }} />
              <Layer id="plots-outline" type="line" paint={{
                'line-color': ['case', ['==', ['get', 'valid'], 1], '#1b5e20', '#b71c1c'],
                'line-width': 1,
              }} />
            </Source>

            {/* Draft preview */}
            {draftPreview && (
              <Source id="draft" type="geojson" data={draftPreview}>
                {draftPreview.geometry.type === 'Polygon' && (
                  <Layer id="draft-fill" type="fill" paint={{ 'fill-color': '#d33', 'fill-opacity': 0.18 }} />
                )}
                <Layer id="draft-line" type="line"
                  paint={{ 'line-color': '#d33', 'line-width': 2, 'line-dasharray': [2, 2] }} />
              </Source>
            )}

            {/* Draft vertices */}
            {isDrawing && draft.map((v, i) => (
              <Marker key={`d${i}`} longitude={v[0]} latitude={v[1]} anchor="center">
                <div style={vertexDot('#d33')} />
              </Marker>
            ))}

            {/* Edit handles */}
            {editVerts && editVerts.map((v, i) => (
              <Marker
                key={`e${i}`}
                longitude={v[0]}
                latitude={v[1]}
                anchor="center"
                draggable
                onDrag={(ev) => {
                  setEditVerts((prev) => {
                    if (!prev) return prev;
                    const next = prev.slice();
                    next[i] = [ev.lngLat.lng, ev.lngLat.lat];
                    return next;
                  });
                }}
              >
                <div style={vertexDot('#1976d2')} />
              </Marker>
            ))}
          </Map>
        </div>

        <Sidebar
          visible={visible}
          setVisible={setVisible}
          mainPlot={mainPlot}
          roads={roadList}
          reserved={reservedList}
          suggestive={suggestiveList}
          selection={selection}
          setSelection={(s) => { setSelection(s); setEditVerts(null); }}
          selected={selected}
          editVerts={editVerts}
          startVertexEdit={startVertexEdit}
          saveVertexEdit={() => void saveVertexEdit()}
          cancelVertexEdit={cancelVertexEdit}
          deleteSelected={() => void deleteSelected()}
          saveRoadProps={saveRoadProps}
          saveReservedProps={saveReservedProps}
          saveSuggestiveProps={saveSuggestiveProps}
          busy={busy}
          plotsCount={activeRun?.plots.length ?? 0}
          hasMainPlot={!!mainPlot}
          genParams={genParams}
          setGenParams={setGenParams}
          activeRun={activeRun}
          allRunsCount={allRuns.length}
          runGenerate={() => void runGenerate()}
          commitRun={() => void commitRun()}
          discardRun={() => void discardRun()}
        />
      </div>

      <p style={{ marginTop: '1rem', color: '#888', fontSize: 13 }}>
        Drawing: click to add a point, Enter or double-click to finish, Backspace to undo last, Esc to cancel.
      </p>
    </div>
  );
}

function drawHint(t: Tool): string {
  switch (t) {
    case 'draw-main-plot': return 'Drawing main plot polygon. Click to add vertices, Enter to save.';
    case 'draw-road': return 'Drawing road centerline. Click to add points, Enter to save.';
    case 'draw-reserved': return 'Drawing reserved area polygon. Click to add vertices, Enter to save.';
    case 'draw-suggestive': return 'Drawing suggestive line. Click to add points, Enter to save.';
    default: return '';
  }
}

type ToolbarProps = {
  tool: Tool;
  setTool: (t: Tool) => void;
  busy: boolean;
  hasMain: boolean;
  onFinish: () => void;
  draftCount: number;
};

function Toolbar({ tool, setTool, busy, hasMain, onFinish, draftCount }: ToolbarProps) {
  const btn = (label: string, t: Tool, disabled?: boolean) => (
    <button
      onClick={() => setTool(t)}
      disabled={disabled || busy}
      style={{
        padding: '0.4rem 0.8rem',
        background: tool === t ? '#1976d2' : '#fff',
        color: tool === t ? '#fff' : '#222',
        border: '1px solid #bbb',
        borderRadius: 4,
        cursor: 'pointer',
      }}
    >
      {label}
    </button>
  );
  return (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', margin: '1rem 0' }}>
      {btn('Select', 'select')}
      {btn(hasMain ? 'Redraw main plot' : 'Draw main plot', 'draw-main-plot')}
      {btn('Draw road', 'draw-road')}
      {btn('Draw reserved area', 'draw-reserved')}
      {btn('Draw suggestive line', 'draw-suggestive')}
      {tool !== 'select' && (
        <button onClick={onFinish} disabled={busy || draftCount < 2} style={{ padding: '0.4rem 0.8rem' }}>
          Finish ({draftCount})
        </button>
      )}
    </div>
  );
}

type SidebarProps = {
  visible: LayersVisible;
  setVisible: (v: LayersVisible) => void;
  mainPlot: MainPlot | null;
  roads: Road[];
  reserved: ReservedArea[];
  suggestive: SuggestiveLine[];
  selection: Selection | null;
  setSelection: (s: Selection | null) => void;
  selected:
    | { kind: 'main-plot'; value: MainPlot }
    | { kind: 'road'; value: Road }
    | { kind: 'reserved'; value: ReservedArea }
    | { kind: 'suggestive'; value: SuggestiveLine }
    | null;
  editVerts: LonLat[] | null;
  startVertexEdit: () => void;
  saveVertexEdit: () => void;
  cancelVertexEdit: () => void;
  deleteSelected: () => void;
  saveRoadProps: (patch: Partial<Road>) => void;
  saveReservedProps: (patch: Partial<ReservedArea>) => void;
  saveSuggestiveProps: (patch: Partial<SuggestiveLine>) => void;
  busy: boolean;
  plotsCount: number;
  hasMainPlot: boolean;
  genParams: GenerationParams;
  setGenParams: (p: GenerationParams) => void;
  activeRun: GenerationRun | null;
  allRunsCount: number;
  runGenerate: () => void;
  commitRun: () => void;
  discardRun: () => void;
};

function Sidebar(p: SidebarProps) {
  const sectionStyle: React.CSSProperties = { border: '1px solid #ddd', borderRadius: 6, padding: '0.75rem', background: '#fafafa' };
  const headerRow: React.CSSProperties = { display: 'flex', justifyContent: 'space-between', alignItems: 'center' };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={sectionStyle}>
        <h3 style={{ margin: '0 0 0.5rem 0' }}>Layers</h3>
        <LayerToggle label="Main plot" checked={p.visible.mainPlot}
          onChange={(c) => p.setVisible({ ...p.visible, mainPlot: c })} count={p.mainPlot ? 1 : 0} />
        <LayerToggle label="Roads" checked={p.visible.roads}
          onChange={(c) => p.setVisible({ ...p.visible, roads: c })} count={p.roads.length} />
        <LayerToggle label="Reserved areas" checked={p.visible.reserved}
          onChange={(c) => p.setVisible({ ...p.visible, reserved: c })} count={p.reserved.length} />
        <LayerToggle label="Suggestive lines" checked={p.visible.suggestive}
          onChange={(c) => p.setVisible({ ...p.visible, suggestive: c })} count={p.suggestive.length} />
        <LayerToggle label="Generated plots" checked={p.visible.plots}
          onChange={(c) => p.setVisible({ ...p.visible, plots: c })} count={p.plotsCount} />
      </div>

      <div style={sectionStyle}>
        <h3 style={{ margin: '0 0 0.5rem 0' }}>Generate</h3>
        <GeneratePanel
          params={p.genParams}
          setParams={p.setGenParams}
          activeRun={p.activeRun}
          allRunsCount={p.allRunsCount}
          hasMainPlot={p.hasMainPlot}
          busy={p.busy}
          onGenerate={p.runGenerate}
          onCommit={p.commitRun}
          onDiscard={p.discardRun}
        />
      </div>

      <div style={sectionStyle}>
        <div style={headerRow}>
          <h3 style={{ margin: 0 }}>Features</h3>
        </div>
        <FeatureGroup label="Roads" items={p.roads.map((r) => ({
          id: r.id, label: r.name || `${r.class} road`, sub: `${r.lanes} lanes · ${r.widthMeters} m`,
        }))} kind="road" selection={p.selection} setSelection={p.setSelection} />
        <FeatureGroup label="Reserved" items={p.reserved.map((r) => ({
          id: r.id, label: r.name || r.kind, sub: r.kind,
        }))} kind="reserved" selection={p.selection} setSelection={p.setSelection} />
        <FeatureGroup label="Suggestive" items={p.suggestive.map((r) => ({
          id: r.id, label: r.name || 'suggestive line', sub: `w=${r.weight} tol=${r.toleranceMeters}m`,
        }))} kind="suggestive" selection={p.selection} setSelection={p.setSelection} />
        {p.mainPlot && (
          <FeatureGroup label="Main plot" items={[{
            id: p.mainPlot.id, label: 'Main plot', sub: formatArea(p.mainPlot.areaSqM),
          }]} kind="main-plot" selection={p.selection} setSelection={p.setSelection} />
        )}
      </div>

      {p.selected && (
        <div style={sectionStyle}>
          <h3 style={{ marginTop: 0 }}>Selected: {p.selected.kind}</h3>
          {p.selected.kind === 'road' && (
            <RoadForm road={p.selected.value} disabled={p.busy} onPatch={p.saveRoadProps} />
          )}
          {p.selected.kind === 'reserved' && (
            <ReservedForm value={p.selected.value} disabled={p.busy} onPatch={p.saveReservedProps} />
          )}
          {p.selected.kind === 'suggestive' && (
            <SuggestiveForm value={p.selected.value} disabled={p.busy} onPatch={p.saveSuggestiveProps} />
          )}
          {p.selected.kind === 'main-plot' && (
            <p style={{ color: '#444' }}>Area: {formatArea(p.selected.value.areaSqM)}</p>
          )}

          <div style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
            {!p.editVerts && <button onClick={p.startVertexEdit} disabled={p.busy}>Edit vertices</button>}
            {p.editVerts && <button onClick={p.saveVertexEdit} disabled={p.busy}>Save vertices</button>}
            {p.editVerts && <button onClick={p.cancelVertexEdit} disabled={p.busy}>Cancel</button>}
            <button onClick={p.deleteSelected} disabled={p.busy} style={{ color: '#c33' }}>Delete</button>
          </div>
        </div>
      )}
    </div>
  );
}

function LayerToggle({ label, checked, onChange, count }: {
  label: string; checked: boolean; onChange: (c: boolean) => void; count: number;
}) {
  return (
    <label style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0' }}>
      <span>
        <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} /> {label}
      </span>
      <span style={{ color: '#888' }}>{count}</span>
    </label>
  );
}

function FeatureGroup({ label, items, kind, selection, setSelection }: {
  label: string;
  items: { id: string; label: string; sub: string }[];
  kind: SelectionKind;
  selection: Selection | null;
  setSelection: (s: Selection | null) => void;
}) {
  if (items.length === 0) return null;
  return (
    <div style={{ marginTop: 8 }}>
      <div style={{ fontSize: 12, color: '#666', textTransform: 'uppercase', letterSpacing: 0.5 }}>{label}</div>
      <ul style={{ listStyle: 'none', padding: 0, margin: '4px 0' }}>
        {items.map((it) => {
          const isSel = selection?.kind === kind && selection.id === it.id;
          return (
            <li key={it.id}>
              <button
                onClick={() => setSelection(isSel ? null : { kind, id: it.id })}
                style={{
                  width: '100%', textAlign: 'left', padding: '4px 6px',
                  background: isSel ? '#e3f2fd' : 'transparent',
                  border: '1px solid transparent', borderRadius: 4, cursor: 'pointer',
                }}
              >
                <div>{it.label}</div>
                <div style={{ fontSize: 12, color: '#777' }}>{it.sub}</div>
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function RoadForm({ road, disabled, onPatch }: { road: Road; disabled: boolean; onPatch: (p: Partial<Road>) => void }) {
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      <Field label="Name">
        <input value={road.name ?? ''} disabled={disabled}
          onChange={(e) => onPatch({ name: e.target.value })} />
      </Field>
      <Field label="Class">
        <select value={road.class} disabled={disabled}
          onChange={(e) => {
            const cls = e.target.value as RoadClass;
            onPatch({ class: cls, widthMeters: defaultRoadWidthMeters(cls), lanes: defaultRoadLanes(cls) });
          }}>
          {ROAD_CLASSES.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
      </Field>
      <Field label="Lanes">
        <input type="number" min={1} max={12} value={road.lanes} disabled={disabled}
          onChange={(e) => onPatch({ lanes: Number(e.target.value) })} />
      </Field>
      <Field label="Width (m)">
        <input type="number" step={0.5} min={1} value={road.widthMeters} disabled={disabled}
          onChange={(e) => onPatch({ widthMeters: Number(e.target.value) })} />
      </Field>
      <label><input type="checkbox" checked={road.hasFootpath} disabled={disabled}
        onChange={(e) => onPatch({ hasFootpath: e.target.checked })} /> Footpath</label>
      <label><input type="checkbox" checked={road.hasBikepath} disabled={disabled}
        onChange={(e) => onPatch({ hasBikepath: e.target.checked })} /> Bike path</label>
    </div>
  );
}

function ReservedForm({ value, disabled, onPatch }: {
  value: ReservedArea; disabled: boolean; onPatch: (p: Partial<ReservedArea>) => void;
}) {
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      <Field label="Name">
        <input value={value.name ?? ''} disabled={disabled}
          onChange={(e) => onPatch({ name: e.target.value })} />
      </Field>
      <Field label="Kind">
        <select value={value.kind} disabled={disabled}
          onChange={(e) => onPatch({ kind: e.target.value as ReservedKind })}>
          {RESERVED_KINDS.map((k) => <option key={k} value={k}>{k}</option>)}
        </select>
      </Field>
    </div>
  );
}

function SuggestiveForm({ value, disabled, onPatch }: {
  value: SuggestiveLine; disabled: boolean; onPatch: (p: Partial<SuggestiveLine>) => void;
}) {
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      <Field label="Name">
        <input value={value.name ?? ''} disabled={disabled}
          onChange={(e) => onPatch({ name: e.target.value })} />
      </Field>
      <Field label="Weight">
        <input type="number" step={0.1} min={0} max={100} value={value.weight} disabled={disabled}
          onChange={(e) => onPatch({ weight: Number(e.target.value) })} />
      </Field>
      <Field label="Tolerance (m)">
        <input type="number" step={5} min={0} max={10000} value={value.toleranceMeters} disabled={disabled}
          onChange={(e) => onPatch({ toleranceMeters: Number(e.target.value) })} />
      </Field>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <span style={{ fontSize: 12, color: '#555' }}>{label}</span>
      {children}
    </label>
  );
}

function GeneratePanel({
  params, setParams, activeRun, allRunsCount, hasMainPlot, busy,
  onGenerate, onCommit, onDiscard,
}: {
  params: GenerationParams;
  setParams: (p: GenerationParams) => void;
  activeRun: GenerationRun | null;
  allRunsCount: number;
  hasMainPlot: boolean;
  busy: boolean;
  onGenerate: () => void;
  onCommit: () => void;
  onDiscard: () => void;
}) {
  const num = (k: keyof GenerationParams) => (
    <input type="number" value={params[k]} disabled={busy}
      onChange={(e) => setParams({ ...params, [k]: Number(e.target.value) })} />
  );
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      <Field label="Target plot area (m²)">{num('targetPlotAreaSqM')}</Field>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        <Field label="Min area">{num('minPlotAreaSqM')}</Field>
        <Field label="Max area">{num('maxPlotAreaSqM')}</Field>
      </div>
      <Field label="Min road frontage (m)">{num('minRoadFrontageMeters')}</Field>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        <Field label="Seed">{num('seed')}</Field>
        <Field label="Rotation (rad)">{num('gridRotationRadians')}</Field>
      </div>
      <button onClick={onGenerate} disabled={busy || !hasMainPlot}
        style={{ padding: '0.4rem 0.8rem', background: '#1976d2', color: '#fff', border: 'none', borderRadius: 4 }}>
        {busy ? 'Working…' : 'Generate'}
      </button>
      {!hasMainPlot && <p style={{ color: '#a60', fontSize: 12, margin: 0 }}>Draw a main plot first.</p>}
      {activeRun && (
        <div style={{ borderTop: '1px solid #ddd', paddingTop: 8 }}>
          <div style={{ fontSize: 12, color: '#666' }}>
            Run <code>{activeRun.id.slice(0, 8)}</code> — <strong>{activeRun.status}</strong>
            {' · '}{allRunsCount} total
          </div>
          <ul style={{ margin: '6px 0 8px', paddingLeft: 16, fontSize: 13, color: '#333' }}>
            <li>Plots: {activeRun.plots.length} ({activeRun.stats.plotsValid} valid, {activeRun.stats.plotsInvalid} flagged)</li>
            <li>Plot area: {Math.round(activeRun.stats.totalPlotAreaSqM).toLocaleString()} m²</li>
            <li>Road area: {Math.round(activeRun.stats.totalRoadAreaSqM).toLocaleString()} m²</li>
            <li>Reserved area: {Math.round(activeRun.stats.totalReservedAreaSqM).toLocaleString()} m²</li>
            <li>Main plot: {Math.round(activeRun.stats.mainPlotAreaSqM).toLocaleString()} m²</li>
          </ul>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {activeRun.status === 'preview' && (
              <button onClick={onCommit} disabled={busy}>Commit</button>
            )}
            <button onClick={onDiscard} disabled={busy} style={{ color: '#c33' }}>Discard</button>
          </div>
          {activeRun.stats.plotsInvalid > 0 && (
            <details style={{ marginTop: 6 }}>
              <summary style={{ cursor: 'pointer', fontSize: 12 }}>Show flagged ({activeRun.stats.plotsInvalid})</summary>
              <ul style={{ paddingLeft: 16, fontSize: 12, color: '#a30' }}>
                {activeRun.plots.filter((pl) => !pl.validationPassed).slice(0, 30).map((pl) => (
                  <li key={pl.id}>block {pl.blockIndex}: {pl.validationReason}</li>
                ))}
              </ul>
            </details>
          )}
        </div>
      )}
    </div>
  );
}
