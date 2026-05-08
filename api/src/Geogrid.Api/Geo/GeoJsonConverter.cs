using Geogrid.Api.Contracts;
using NetTopologySuite.Geometries;

namespace Geogrid.Api.Geo;

public static class GeoJsonConverter
{
    public static readonly GeometryFactory Factory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static Polygon ToPolygon(GeoJsonPolygon g)
    {
        if (!string.Equals(g.Type, "Polygon", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Geometry type must be 'Polygon'.");
        if (g.Coordinates is null || g.Coordinates.Length == 0)
            throw new ArgumentException("Polygon coordinates required.");

        var rings = new LinearRing[g.Coordinates.Length];
        for (var r = 0; r < g.Coordinates.Length; r++)
        {
            var ringCoords = g.Coordinates[r];
            if (ringCoords.Length < 4)
                throw new ArgumentException("Each ring must have at least 4 positions (closed).");
            var coords = new Coordinate[ringCoords.Length];
            for (var i = 0; i < ringCoords.Length; i++)
            {
                var pos = ringCoords[i];
                if (pos.Length < 2) throw new ArgumentException("Each position needs [lon, lat].");
                coords[i] = new Coordinate(pos[0], pos[1]);
            }
            if (!coords[0].Equals2D(coords[^1]))
                throw new ArgumentException("Ring must be closed (first == last).");
            rings[r] = Factory.CreateLinearRing(coords);
        }

        var shell = rings[0];
        var holes = rings.Length > 1 ? rings[1..] : Array.Empty<LinearRing>();
        var polygon = Factory.CreatePolygon(shell, holes);
        polygon.SRID = 4326;
        return polygon;
    }

    public static LineString ToLineString(GeoJsonLineString g)
    {
        if (!string.Equals(g.Type, "LineString", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Geometry type must be 'LineString'.");
        if (g.Coordinates is null || g.Coordinates.Length < 2)
            throw new ArgumentException("LineString requires at least 2 positions.");

        var coords = new Coordinate[g.Coordinates.Length];
        for (var i = 0; i < g.Coordinates.Length; i++)
        {
            var pos = g.Coordinates[i];
            if (pos.Length < 2) throw new ArgumentException("Each position needs [lon, lat].");
            coords[i] = new Coordinate(pos[0], pos[1]);
        }
        var line = Factory.CreateLineString(coords);
        line.SRID = 4326;
        return line;
    }

    public static GeoJsonPolygon FromPolygon(Polygon polygon)
    {
        var rings = new List<double[][]>(1 + polygon.NumInteriorRings)
        {
            RingToArray(polygon.ExteriorRing.Coordinates),
        };
        for (var i = 0; i < polygon.NumInteriorRings; i++)
            rings.Add(RingToArray(polygon.GetInteriorRingN(i).Coordinates));
        return new GeoJsonPolygon("Polygon", rings.ToArray());
    }

    public static GeoJsonLineString FromLineString(LineString line)
    {
        var arr = new double[line.NumPoints][];
        for (var i = 0; i < line.NumPoints; i++)
        {
            var c = line.GetCoordinateN(i);
            arr[i] = new[] { c.X, c.Y };
        }
        return new GeoJsonLineString("LineString", arr);
    }

    private static double[][] RingToArray(Coordinate[] coords)
    {
        var arr = new double[coords.Length][];
        for (var i = 0; i < coords.Length; i++)
            arr[i] = new[] { coords[i].X, coords[i].Y };
        return arr;
    }

    /// <summary>Approximate area in m² of a WGS84 polygon via equirectangular projection at centroid.</summary>
    public static double ApproximateAreaSqMeters(Polygon polygon)
    {
        var centroid = polygon.Centroid;
        var lat0 = centroid.Y * Math.PI / 180.0;
        const double R = 6_378_137.0;
        var cosLat = Math.Cos(lat0);

        Coordinate[] Project(Coordinate[] cs)
        {
            var p = new Coordinate[cs.Length];
            for (var i = 0; i < cs.Length; i++)
            {
                var x = R * (cs[i].X * Math.PI / 180.0) * cosLat;
                var y = R * (cs[i].Y * Math.PI / 180.0);
                p[i] = new Coordinate(x, y);
            }
            return p;
        }

        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
        var shell = factory.CreateLinearRing(Project(polygon.ExteriorRing.Coordinates));
        var holes = new LinearRing[polygon.NumInteriorRings];
        for (var i = 0; i < polygon.NumInteriorRings; i++)
            holes[i] = factory.CreateLinearRing(Project(polygon.GetInteriorRingN(i).Coordinates));
        return factory.CreatePolygon(shell, holes).Area;
    }
}
