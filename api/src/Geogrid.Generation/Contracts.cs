using NetTopologySuite.Geometries;

namespace Geogrid.Generation;

public record GenerationInputs(
    Polygon MainPlotWgs,
    IReadOnlyList<RoadInput> Roads,
    IReadOnlyList<Polygon> ReservedAreasWgs);

public record RoadInput(LineString GeometryWgs, double WidthMeters);

public record GenerationParams(
    double TargetPlotAreaSqM = 600,
    double MinPlotAreaSqM = 200,
    double MaxPlotAreaSqM = 2000,
    double MinRoadFrontageMeters = 8,
    int Seed = 0,
    double GridRotationRadians = 0);

public record GeneratedPlot(
    int BlockIndex,
    Polygon GeometryWgs,
    double AreaSqM,
    double RoadFrontageMeters,
    bool ValidationPassed,
    string? ValidationReason);

public record GenerationResult(
    IReadOnlyList<Polygon> BlocksWgs,
    IReadOnlyList<GeneratedPlot> Plots,
    GenerationStats Stats);

public record GenerationStats(
    double MainPlotAreaSqM,
    double TotalPlotAreaSqM,
    double TotalReservedAreaSqM,
    double TotalRoadAreaSqM,
    int PlotsValid,
    int PlotsInvalid);

public interface IPlotGenerator
{
    GenerationResult Generate(GenerationInputs inputs, GenerationParams parameters);
}
