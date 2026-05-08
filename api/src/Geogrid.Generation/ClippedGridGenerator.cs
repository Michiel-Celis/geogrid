using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Operation.Polygonize;

namespace Geogrid.Generation;

/// <summary>
/// v1 "boring" subdivider:
///   1. Project everything to a local meter grid.
///   2. Buffer each road into a corridor polygon, union all roads + reserved areas → "blockers".
///   3. Subtract blockers from the main plot → blocks (one or more polygons).
///   4. For each block, overlay an axis-aligned grid sized to <see cref="GenerationParams.TargetPlotAreaSqM"/> and clip cells to the block.
///   5. Re-project each plot back to WGS84.
///   6. Compute road frontage by intersecting the plot boundary with the union of road buffer outlines and validate.
/// </summary>
public sealed class ClippedGridGenerator : IPlotGenerator
{
    public GenerationResult Generate(GenerationInputs inputs, GenerationParams parameters)
    {
        var centroid = inputs.MainPlotWgs.Centroid;
        var proj = new LocalProjection(centroid.Y, centroid.X);
        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();

        var mainLocal = (Polygon)Project(inputs.MainPlotWgs, proj, factory);
        var mainArea = mainLocal.Area;

        // Build buffered road polygons in local meters.
        var roadBuffers = new List<Geometry>();
        foreach (var r in inputs.Roads)
        {
            var line = (LineString)Project(r.GeometryWgs, proj, factory);
            var buf = line.Buffer(r.WidthMeters / 2.0, new BufferParameters { EndCapStyle = EndCapStyle.Flat, JoinStyle = JoinStyle.Round });
            if (!buf.IsEmpty) roadBuffers.Add(buf);
        }
        var roadsUnion = roadBuffers.Count == 0 ? factory.CreatePolygon() : UnionAll(roadBuffers);

        // Reserved areas in local meters.
        var reservedLocal = inputs.ReservedAreasWgs
            .Select(p => (Geometry)Project(p, proj, factory))
            .ToList();
        var reservedUnion = reservedLocal.Count == 0 ? factory.CreatePolygon() : UnionAll(reservedLocal);

        var blockers = roadsUnion.Union(reservedUnion);
        var blockSpace = mainLocal.Difference(blockers);

        var blocks = ExtractPolygons(blockSpace).ToList();

        // Generate plots per block.
        var allPlots = new List<GeneratedPlot>();
        var rng = new Random(parameters.Seed);
        var rotation = parameters.GridRotationRadians;

        for (var bi = 0; bi < blocks.Count; bi++)
        {
            var block = blocks[bi];
            if (block.Area < parameters.MinPlotAreaSqM) continue;

            var cellSize = Math.Sqrt(parameters.TargetPlotAreaSqM);
            var blockPlots = SubdivideBlock(block, cellSize, rotation, factory, rng);

            foreach (var plotLocal in blockPlots)
            {
                if (plotLocal.IsEmpty || plotLocal.Area < 1) continue;
                var areaLocal = plotLocal.Area;
                var frontage = ComputeRoadFrontageMeters(plotLocal, roadsUnion);
                var (passed, reason) = Validate(areaLocal, frontage, parameters);

                var plotWgs = (Polygon)Unproject(plotLocal, proj, factory);
                plotWgs.SRID = 4326;
                allPlots.Add(new GeneratedPlot(bi, plotWgs, areaLocal, frontage, passed, reason));
            }
        }

        var blocksWgs = blocks
            .Select(b => { var p = (Polygon)Unproject(b, proj, factory); p.SRID = 4326; return p; })
            .ToList();

        var stats = new GenerationStats(
            MainPlotAreaSqM: mainArea,
            TotalPlotAreaSqM: allPlots.Sum(p => p.AreaSqM),
            TotalReservedAreaSqM: reservedUnion.Area,
            TotalRoadAreaSqM: roadsUnion.Difference(reservedUnion).Area,
            PlotsValid: allPlots.Count(p => p.ValidationPassed),
            PlotsInvalid: allPlots.Count(p => !p.ValidationPassed));

        return new GenerationResult(blocksWgs, allPlots, stats);
    }

    private static (bool passed, string? reason) Validate(double areaSqM, double frontage, GenerationParams p)
    {
        if (areaSqM < p.MinPlotAreaSqM) return (false, $"area {areaSqM:F0} m² < min {p.MinPlotAreaSqM:F0}");
        if (areaSqM > p.MaxPlotAreaSqM) return (false, $"area {areaSqM:F0} m² > max {p.MaxPlotAreaSqM:F0}");
        if (frontage < p.MinRoadFrontageMeters) return (false, $"road frontage {frontage:F1} m < min {p.MinRoadFrontageMeters:F0}");
        return (true, null);
    }

    private static IEnumerable<Polygon> SubdivideBlock(Polygon block, double cellSize, double rotation, GeometryFactory factory, Random _)
    {
        // Rotate the block backwards, grid-clip in the rotated frame, rotate forward.
        var center = block.Centroid;
        var cos = Math.Cos(-rotation);
        var sin = Math.Sin(-rotation);
        var blockRot = (Polygon)RotateAround(block, center.X, center.Y, cos, sin, factory);
        var env = blockRot.EnvelopeInternal;

        // Snap origin to multiples of cellSize so the grid is stable across runs.
        var x0 = Math.Floor(env.MinX / cellSize) * cellSize;
        var y0 = Math.Floor(env.MinY / cellSize) * cellSize;

        var cosBack = Math.Cos(rotation);
        var sinBack = Math.Sin(rotation);

        var results = new List<Polygon>();
        for (var x = x0; x < env.MaxX; x += cellSize)
        {
            for (var y = y0; y < env.MaxY; y += cellSize)
            {
                var cell = factory.CreatePolygon([
                    new Coordinate(x, y),
                    new Coordinate(x + cellSize, y),
                    new Coordinate(x + cellSize, y + cellSize),
                    new Coordinate(x, y + cellSize),
                    new Coordinate(x, y),
                ]);
                if (!cell.Intersects(blockRot)) continue;
                var clipped = blockRot.Intersection(cell);
                foreach (var poly in ExtractPolygons(clipped))
                {
                    var rotatedBack = (Polygon)RotateAround(poly, center.X, center.Y, cosBack, sinBack, factory);
                    results.Add(rotatedBack);
                }
            }
        }
        return results;
    }

    private static double ComputeRoadFrontageMeters(Polygon plot, Geometry roadsUnion)
    {
        if (roadsUnion.IsEmpty) return 0;
        var boundary = plot.Boundary;
        // Frontage = portion of plot boundary that lies within the road buffer (within ~5 cm tolerance).
        var inRoad = boundary.Intersection(roadsUnion);
        return inRoad.Length;
    }

    private static Geometry Project(Geometry g, LocalProjection proj, GeometryFactory factory)
        => Transform(g, factory, (x, y) => proj.ToLocal(x, y));

    private static Geometry Unproject(Geometry g, LocalProjection proj, GeometryFactory factory)
        => Transform(g, factory, (x, y) => proj.ToWgs(x, y));

    private static Geometry RotateAround(Geometry g, double cx, double cy, double cos, double sin, GeometryFactory factory)
        => Transform(g, factory, (x, y) =>
        {
            var dx = x - cx;
            var dy = y - cy;
            return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
        });

    private static Geometry Transform(Geometry g, GeometryFactory factory, Func<double, double, (double, double)> fn)
    {
        switch (g)
        {
            case Polygon p:
                {
                    var shell = TransformRing(p.ExteriorRing, factory, fn);
                    var holes = new LinearRing[p.NumInteriorRings];
                    for (var i = 0; i < p.NumInteriorRings; i++)
                        holes[i] = TransformRing(p.GetInteriorRingN(i), factory, fn);
                    return factory.CreatePolygon(shell, holes);
                }
            case LineString l:
                {
                    var coords = TransformCoords(l.Coordinates, fn);
                    return factory.CreateLineString(coords);
                }
            case MultiPolygon mp:
                {
                    var arr = new Polygon[mp.NumGeometries];
                    for (var i = 0; i < mp.NumGeometries; i++) arr[i] = (Polygon)Transform(mp.GetGeometryN(i), factory, fn);
                    return factory.CreateMultiPolygon(arr);
                }
            case GeometryCollection gc:
                {
                    var arr = new Geometry[gc.NumGeometries];
                    for (var i = 0; i < gc.NumGeometries; i++) arr[i] = Transform(gc.GetGeometryN(i), factory, fn);
                    return factory.CreateGeometryCollection(arr);
                }
            case Point pt:
                {
                    var (x, y) = fn(pt.X, pt.Y);
                    return factory.CreatePoint(new Coordinate(x, y));
                }
            default:
                throw new NotSupportedException($"Unsupported geometry: {g.GeometryType}");
        }
    }

    private static LinearRing TransformRing(LineString ring, GeometryFactory factory, Func<double, double, (double, double)> fn)
        => factory.CreateLinearRing(TransformCoords(ring.Coordinates, fn));

    private static Coordinate[] TransformCoords(Coordinate[] coords, Func<double, double, (double, double)> fn)
    {
        var arr = new Coordinate[coords.Length];
        for (var i = 0; i < coords.Length; i++)
        {
            var (x, y) = fn(coords[i].X, coords[i].Y);
            arr[i] = new Coordinate(x, y);
        }
        return arr;
    }

    private static Geometry UnionAll(IList<Geometry> gs)
    {
        var u = gs[0];
        for (var i = 1; i < gs.Count; i++) u = u.Union(gs[i]);
        return u;
    }

    private static IEnumerable<Polygon> ExtractPolygons(Geometry g)
    {
        switch (g)
        {
            case Polygon p when !p.IsEmpty: yield return p; break;
            case MultiPolygon mp:
                for (var i = 0; i < mp.NumGeometries; i++)
                    if (mp.GetGeometryN(i) is Polygon pp && !pp.IsEmpty) yield return pp;
                break;
            case GeometryCollection gc:
                for (var i = 0; i < gc.NumGeometries; i++)
                    foreach (var p in ExtractPolygons(gc.GetGeometryN(i))) yield return p;
                break;
        }
    }

    // (Unused for v1 — kept available for future Polygonizer-based block extraction.)
    public static IEnumerable<Polygon> PolygonizeLines(IEnumerable<LineString> lines)
    {
        var polygonizer = new Polygonizer();
        foreach (var l in lines) polygonizer.Add(l);
        return polygonizer.GetPolygons().OfType<Polygon>();
    }
}
