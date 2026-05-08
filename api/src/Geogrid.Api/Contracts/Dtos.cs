using System.ComponentModel.DataAnnotations;

namespace Geogrid.Api.Contracts;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, Guid UserId, string Email);

public record CreateProjectRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(1024, 999999)] int Srid,
    [Range(-90, 90)] double CenterLat,
    [Range(-180, 180)] double CenterLon);

public record UpdateProjectRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(1024, 999999)] int Srid,
    [Range(-90, 90)] double CenterLat,
    [Range(-180, 180)] double CenterLon);

public record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    int Srid,
    double CenterLat,
    double CenterLon,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// GeoJSON Polygon shape: { type: "Polygon", coordinates: [[ [lon,lat], ... ]] }
public record GeoJsonPolygon(string Type, double[][][] Coordinates);

// GeoJSON LineString: { type: "LineString", coordinates: [ [lon,lat], ... ] }
public record GeoJsonLineString(string Type, double[][] Coordinates);

public record MainPlotRequest([Required] GeoJsonPolygon Geometry);

public record MainPlotResponse(
    Guid Id,
    Guid ProjectId,
    GeoJsonPolygon Geometry,
    double AreaSqM,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record RoadRequest(
    [MaxLength(200)] string? Name,
    [Required, MaxLength(40)] string Class,
    [Range(1, 12)] int Lanes,
    [Range(1.0, 200.0)] double WidthMeters,
    bool HasFootpath,
    bool HasBikepath,
    [Required] GeoJsonLineString Geometry);

public record RoadResponse(
    Guid Id,
    Guid ProjectId,
    string? Name,
    string Class,
    int Lanes,
    double WidthMeters,
    bool HasFootpath,
    bool HasBikepath,
    GeoJsonLineString Geometry,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ReservedAreaRequest(
    [MaxLength(200)] string? Name,
    [Required, MaxLength(40)] string Kind,
    [Required] GeoJsonPolygon Geometry);

public record ReservedAreaResponse(
    Guid Id,
    Guid ProjectId,
    string? Name,
    string Kind,
    GeoJsonPolygon Geometry,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record SuggestiveLineRequest(
    [MaxLength(200)] string? Name,
    [Range(0.0, 100.0)] double Weight,
    [Range(0.0, 10000.0)] double ToleranceMeters,
    [Required] GeoJsonLineString Geometry);

public record SuggestiveLineResponse(
    Guid Id,
    Guid ProjectId,
    string? Name,
    double Weight,
    double ToleranceMeters,
    GeoJsonLineString Geometry,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record GenerationRequest(
    [Range(50, 1_000_000)] double TargetPlotAreaSqM = 600,
    [Range(10, 1_000_000)] double MinPlotAreaSqM = 200,
    [Range(50, 5_000_000)] double MaxPlotAreaSqM = 2000,
    [Range(0, 1000)] double MinRoadFrontageMeters = 8,
    [Range(0, int.MaxValue)] int Seed = 0,
    [Range(-3.15, 3.15)] double GridRotationRadians = 0);

public record GenerationStatsResponse(
    double MainPlotAreaSqM,
    double TotalPlotAreaSqM,
    double TotalReservedAreaSqM,
    double TotalRoadAreaSqM,
    int PlotsValid,
    int PlotsInvalid);

public record PlotResponse(
    Guid Id,
    Guid GenerationRunId,
    int BlockIndex,
    GeoJsonPolygon Geometry,
    double AreaSqM,
    double RoadFrontageMeters,
    bool ValidationPassed,
    string? ValidationReason);

public record GenerationRunResponse(
    Guid Id,
    Guid ProjectId,
    string Status,
    string Algorithm,
    int Seed,
    GenerationRequest Parameters,
    GenerationStatsResponse Stats,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommittedAt,
    IReadOnlyList<PlotResponse> Plots);
