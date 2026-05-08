# Geogrid — Implementation Plan

A milestone-based plan, ordered so each phase produces something demoable and de-risks the next. No time estimates — sized by relative complexity (S / M / L / XL).

---

## Milestone 0 — Foundations (S)

**Goal:** Working dev environment, empty app shell, CI green.

- [ ] Monorepo layout: `/web` (Vite + React + TS), `/api` (.NET 8 solution), `/db` (SQL migrations), `/docker`
- [ ] `docker-compose.yml`: `postgis/postgis:16-3.4`, API, web (dev profile)
- [ ] .NET solution: `Geogrid.Api`, `Geogrid.Domain`, `Geogrid.Infrastructure`, `Geogrid.Tests`
- [ ] EF Core + Npgsql + NetTopologySuite wired up; first migration creates `projects` table
- [ ] React app boots, calls `/api/health`
- [ ] GitHub Actions: build + test for both sides
- [ ] Linting/formatting: ESLint + Prettier, `dotnet format`, EditorConfig
- [ ] README updated with run instructions

**Exit criteria:** `docker compose up` → web at :5173, API at :5000, DB healthy, one passing test on each side.

---

## Milestone 1 — Auth & Project CRUD (S)

**Goal:** Users can sign in and create/list/delete empty projects.

- [ ] JWT bearer auth (start local accounts; OIDC later)
- [ ] `users`, `projects` tables; project owned by user
- [ ] API: `POST/GET/PUT/DELETE /api/projects`
- [ ] Project includes name, description, **CRS selector** (UTM zone or EPSG code), bounding location (lat/lon for map centering)
- [ ] React: login page, projects list, "new project" wizard (name + click-on-map to set location → auto-suggest UTM zone)
- [ ] Empty project view with map centered on chosen location

**Exit criteria:** Can register, log in, create project, see it on a map.

---

## Milestone 2 — 2D Map & Main Plot Drawing (M)

**Goal:** Draw and persist the main plot polygon.

- [ ] Integrate **MapLibre GL** (`react-map-gl/maplibre`) with OSM or MapTiler tiles
- [ ] Integrate **Mapbox GL Draw** (works with MapLibre) or **Terra Draw**
- [ ] Polygon draw mode with vertex snapping, ESC to cancel, Enter to commit
- [ ] DB: `main_plots(project_id, geom geometry(Polygon, 4326))` + GiST index
- [ ] API: `PUT /api/projects/{id}/main-plot` accepts GeoJSON
- [ ] Server-side validation: `ST_IsValid`, area sanity check, simplify if huge
- [ ] Edit mode: drag vertices, insert/delete vertex
- [ ] Undo/redo stack (client-side, command pattern)
- [ ] Display computed area (m² and ha) using local CRS

**Exit criteria:** Draw a polygon, save, reload page, polygon is still there with correct area.

---

## Milestone 3 — Roads & Reserved Areas (M)

**Goal:** Hard roads with width, plus reserved-area polygons.

- [ ] DB: `roads`, `reserved_areas`, `suggestive_lines` tables
- [ ] Road draw tool: linestring + form (lanes, width, footpath/bikepath flags)
- [ ] Render roads as **buffered polygons** styled by class (Turf `buffer` for preview, NTS for canonical)
- [ ] Reserved area tool: polygon + circle, kind dropdown (town_square / forest / park / pond)
- [ ] Suggestive line tool: distinct dashed style, weight + tolerance fields
- [ ] Layers panel (show/hide/lock each layer)
- [ ] Selection + delete + edit for any feature
- [ ] API endpoints for CRUD on each feature type

**Exit criteria:** A project can contain a main plot, several roads, reserved areas, and suggestive lines, all persisted and re-rendered correctly.

---

## Milestone 4 — Geometry Engine & First Subdivision (L)

**Goal:** One working "boring" subdivision algorithm end-to-end. Proves the whole pipeline.

- [ ] `Geogrid.Generation` project with clean interfaces:
  - `IRoadGenerator`, `IBlockExtractor`, `IPlotSubdivider`, `IGenerationValidator`
- [ ] **Algorithm v1**: clipped grid
  - Take main plot, subtract roads (buffered) and reserved areas
  - Use `ST_Polygonize` for blocks
  - Inside each block, overlay an axis-aligned grid sized to target plot area
  - Clip cells to block
- [ ] `generation_runs` table: stores seed + params JSON + status (preview/committed/discarded)
- [ ] `plots` table with `generation_run_id` FK and `block_id`
- [ ] API: `POST /api/projects/{id}/generate` (sync for now), returns plots as GeoJSON
- [ ] Frontend "Generate" panel with target plot area + commit button
- [ ] Validation pass: each plot has road frontage ≥ N m, area within range; flag failures
- [ ] **Tests**: Testcontainers + PostGIS, property test "sum(plot areas) + roads + reserved ≈ main plot area"

**Exit criteria:** Click Generate → plots appear → commit → reload → plots persist. Validation report lists violations.

---

## Milestone 5 — Organic Generation (L)

**Goal:** The "giraffe pattern" — natural, randomized plots.

- [ ] Add **Poisson-disc sampling** utility (C# + JS)
- [ ] Add **Voronoi + Lloyd relaxation** wrapper around NTS / d3-delaunay
- [ ] Add **Chaikin smoothing** for polygon edges
- [ ] Add **OBB recursive split** (uses NTS `MinimumDiameter`)
- [ ] **Algorithm v2**: organic plots
  1. Poisson seeds in each block, biased toward block centroid
  2. Voronoi → clip to block
  3. 1× Lloyd
  4. Chaikin 2× on edges (skip edges shared with roads to keep frontage straight)
  5. OBB-split any cell over max area
- [ ] Seed input + 🎲 randomize button; same seed = same output (verify with test)
- [ ] Sliders: target area, variance, irregularity, min frontage
- [ ] Client-side preview using same algorithms in JS (Web Worker so UI stays responsive)
- [ ] Multiple generation_runs per project; switcher to compare/commit one

**Exit criteria:** Drawing a parcel + clicking Generate produces organic-looking plots in <1 s for a typical neighborhood, deterministic by seed.

---

## Milestone 6 — Organic Roads from Suggestive Lines (L)

**Goal:** Roads also become natural, guided by user sketches.

- [ ] **Algorithm**: relaxed-Voronoi road network
  - Sample seed points across main plot (Poisson-disc), density driven by param
  - Bias seeds toward suggestive lines (attractor force, weight from line config)
  - Voronoi → take **edges** as road centerlines
  - Snap centerlines to suggestive lines within tolerance
  - Smooth with Catmull-Rom splines
  - Classify roads by length/connectivity (arterial / collector / local) → assign widths
  - Merge near-parallel duplicate edges, drop dead-end stubs below min length
- [ ] Run road generation **before** plot generation in the pipeline
- [ ] User can choose: "use my hard roads only" vs "generate roads from suggestions" vs "both"
- [ ] Preview overlay shows generated roads in a distinct color until committed

**Exit criteria:** Sketch 2 suggestive lines across a parcel → generated road network roughly follows them with natural curves and branching, then plots fill the blocks.

---

## Milestone 7 — Editing After Generation (M)

**Goal:** Users can manually fix what the algorithm got wrong without losing their work.

- [ ] Plot edit: move vertex, merge two adjacent plots, split a plot with a line
- [ ] Road edit: drag vertex, change classification/width, delete segment
- [ ] Re-run validation incrementally on edits
- [ ] "Regenerate this block only" action (preserves rest of project)
- [ ] Undo/redo across all edits including generation runs

**Exit criteria:** Generate → manually merge two plots → reload → edits persisted, validation re-computed.

---

## Milestone 8 — Background Jobs & Performance (M)

**Goal:** Handle large parcels without blocking the UI.

- [ ] **Hangfire** (or hosted service) for generation jobs
- [ ] `POST /generate` becomes async; returns `runId`
- [ ] **SignalR** hub broadcasts progress (`runId` → percent, status messages)
- [ ] Frontend shows progress bar; cancellable
- [ ] **Vector tiles** for plot rendering once a project exceeds ~1000 features (`pg_tileserv` or Martin)
- [ ] Viewport culling: API endpoint takes a bbox, returns only intersecting features
- [ ] Web Worker for client-side preview generation
- [ ] Spatial indexes verified on every geometry column

**Exit criteria:** Generate 5000+ plots without UI freeze, progress visible, map remains smooth when panning.

---

## Milestone 9 — Import / Export (M)

**Goal:** Interoperate with existing tooling.

- [ ] **Import**: GeoJSON, KML, Shapefile (use `NetTopologySuite.IO.*` packages)
- [ ] Importer wizard: pick file → preview on map → choose target layer (main plot / roads / reserved)
- [ ] Reproject on import if source CRS differs
- [ ] **Export**: GeoJSON, KML, Shapefile, **DXF** (for CAD handoff — `netDxf` library)
- [ ] **Print to PDF**: scale (1:500/1:1000), title block, north arrow, legend, scale bar (server-side via headless render or client-side via `html-to-image` + `pdf-lib`)

**Exit criteria:** Round-trip a project: export GeoJSON → re-import into a new project → identical geometry.

---

## Milestone 10 — Quality, History, Sharing (M)

**Goal:** Make it usable by more than one person; safe.

- [ ] **Project versions**: named snapshots (copy all features into a version table)
- [ ] **Audit log** table: who changed what, when
- [ ] **Permissions**: roles (owner/editor/viewer), per-project ACLs
- [ ] **Public read-only share link** (signed token, optional expiry)
- [ ] **Autosave** + IndexedDB local cache for crash recovery
- [ ] **Soft delete** + restore for features
- [ ] OWASP pass: payload size limits on geometry endpoints, validate every GeoJSON, rate limiting, auth on every endpoint, dependency audit
- [ ] **Backups**: documented `pg_dump` schedule + WAL archiving
- [ ] **Observability**: Serilog → file/Seq, OpenTelemetry tracing, `/health` and `/metrics`

**Exit criteria:** Two users can collaborate on one project at the read/write level set by the owner; you can roll back to a previous version.

---

## Milestone 11 — 3D Map (L)

**Goal:** View the design in 3D with terrain.

- [ ] Add MapLibre **terrain** (`setTerrain`) using a free DEM source (MapTiler, AWS Terrain Tiles)
- [ ] Render plots as flat polygons draped on terrain
- [ ] **Extrude buildings** from plot footprint × height attribute (add `building_height_m` to plots)
- [ ] Render roads as 3D ribbons with curbs
- [ ] Camera controls: orbit, fly-through, sun-position slider for shadow study
- [ ] Toggle 2D ↔ 3D preserving selection and viewport
- [ ] (Optional, gated behind a feature flag) Cesium-based view for true globe + 3D Tiles export

**Exit criteria:** Same project viewable in 2D and 3D, terrain visible, plots/roads draped correctly.

---

## Milestone 12 — Polish & Launch (M)

**Goal:** Ready for real users.

- [ ] In-app guided tour (first run)
- [ ] Tooltips on every tool, command palette (Ctrl+K)
- [ ] Keyboard shortcuts documented + cheat sheet
- [ ] i18n scaffolding (`react-i18next`), units toggle (m/ft, m²/ft²/ha/acre)
- [ ] Accessibility pass: keyboard navigation, ARIA labels, color-blind-safe palette
- [ ] Sample projects bundled (suburb, dense urban, rural village)
- [ ] OpenAPI / Swagger UI published
- [ ] User docs site (Docusaurus or similar) with screenshots and recipes
- [ ] Error tracking (Sentry or equivalent)
- [ ] Performance budget verified: TTI < 3s, generation preview < 1s for parcels < 10 ha

**Exit criteria:** A new user, given only the URL, can sign up and produce a generated neighborhood within 5 minutes.

---

## Cross-Cutting Concerns (apply throughout)

- **Testing pyramid**: unit (geometry rules) → integration (Testcontainers + PostGIS) → property-based (FsCheck for invariants) → E2E (Playwright on critical flows)
- **Determinism**: every randomized algorithm takes a `seed`; tests assert same seed → same output
- **CRS discipline**: every geometry crossing the API boundary tagged with SRID; reproject explicitly, never implicitly
- **Feature flags** (`Microsoft.FeatureManagement`) so risky algorithms ship dark and can A/B
- **Documentation**: every algorithm has a short README in its folder explaining inputs, outputs, references

---

## Suggested Build Order Rationale

1. **Plumbing first** (M0–M1) so every later feature has somewhere to live.
2. **Manual drawing before generation** (M2–M3) so generation has real inputs to work on and you can manually demo.
3. **Boring algorithm before fancy one** (M4 grid → M5 organic) so the pipeline is proven before you tune the aesthetics.
4. **Roads after plots** initially with hard roads, then **organic roads** (M6) once the plot pipeline is stable.
5. **Performance after correctness** (M8) — premature optimization hides bugs.
6. **3D last** (M11) — it's a viewer, not a data model change, so it can land any time after M7 without blocking.
