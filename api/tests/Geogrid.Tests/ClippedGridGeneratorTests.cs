using Geogrid.Generation;
using NetTopologySuite.Geometries;

namespace Geogrid.Tests;

public class ClippedGridGeneratorTests
{
    private static readonly GeometryFactory F =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    /// <summary>Build a ~1km square WGS84 polygon centered on (lat, lon).</summary>
    private static Polygon SquareKm(double lat, double lon, double sideKm = 1.0)
    {
        var halfDegLat = (sideKm * 1000) / 2.0 / 111_320.0;
        var halfDegLon = halfDegLat / Math.Cos(lat * Math.PI / 180.0);
        var ring = F.CreateLinearRing(new[]
        {
            new Coordinate(lon - halfDegLon, lat - halfDegLat),
            new Coordinate(lon + halfDegLon, lat - halfDegLat),
            new Coordinate(lon + halfDegLon, lat + halfDegLat),
            new Coordinate(lon - halfDegLon, lat + halfDegLat),
            new Coordinate(lon - halfDegLon, lat - halfDegLat),
        });
        var p = F.CreatePolygon(ring);
        p.SRID = 4326;
        return p;
    }

    [Fact]
    public void EmptyInput_ProducesPlotsCoveringMainPlot()
    {
        var main = SquareKm(52.0, 4.0, sideKm: 0.4); // 400 m square
        var inputs = new GenerationInputs(main, [], []);
        var p = new GenerationParams(TargetPlotAreaSqM: 1000, MinPlotAreaSqM: 50);

        var result = new ClippedGridGenerator().Generate(inputs, p);

        Assert.NotEmpty(result.Plots);
        // Sum of plot areas should approximately equal the main plot area (no roads, no reserved).
        var ratio = result.Stats.TotalPlotAreaSqM / result.Stats.MainPlotAreaSqM;
        Assert.InRange(ratio, 0.98, 1.02);
    }

    [Fact]
    public void ConservationOfArea_PlotsPlusRoadsPlusReservedEqualMainPlot()
    {
        var main = SquareKm(52.0, 4.0, sideKm: 0.5);

        // Diagonal road through the middle.
        var road = F.CreateLineString(new[]
        {
            new Coordinate(3.997, 51.9975),
            new Coordinate(4.003, 52.0025),
        });
        road.SRID = 4326;

        // Small reserved square in one corner.
        var reserved = F.CreatePolygon(F.CreateLinearRing(new[]
        {
            new Coordinate(4.0005, 52.0005),
            new Coordinate(4.0015, 52.0005),
            new Coordinate(4.0015, 52.0015),
            new Coordinate(4.0005, 52.0015),
            new Coordinate(4.0005, 52.0005),
        }));
        reserved.SRID = 4326;

        var inputs = new GenerationInputs(main, [new RoadInput(road, 8)], [reserved]);
        var p = new GenerationParams(TargetPlotAreaSqM: 800, MinPlotAreaSqM: 50);

        var result = new ClippedGridGenerator().Generate(inputs, p);

        var sum = result.Stats.TotalPlotAreaSqM + result.Stats.TotalReservedAreaSqM + result.Stats.TotalRoadAreaSqM;
        var ratio = sum / result.Stats.MainPlotAreaSqM;
        Assert.InRange(ratio, 0.97, 1.03);
    }

    [Fact]
    public void RoadFrontage_PlotsTouchingRoadHaveNonZeroFrontage()
    {
        var main = SquareKm(52.0, 4.0, sideKm: 0.3);
        var road = F.CreateLineString(new[]
        {
            new Coordinate(3.998, 52.0),
            new Coordinate(4.002, 52.0),
        });
        road.SRID = 4326;

        var inputs = new GenerationInputs(main, [new RoadInput(road, 6)], []);
        var p = new GenerationParams(TargetPlotAreaSqM: 400, MinPlotAreaSqM: 50, MinRoadFrontageMeters: 1);

        var result = new ClippedGridGenerator().Generate(inputs, p);

        Assert.True(result.Plots.Any(pl => pl.RoadFrontageMeters > 0),
            "At least some plots should border the road buffer.");
    }
}
