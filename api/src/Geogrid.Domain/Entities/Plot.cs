using NetTopologySuite.Geometries;

namespace Geogrid.Domain.Entities;

public class Plot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid GenerationRunId { get; set; }
    public GenerationRun? GenerationRun { get; set; }

    public int BlockIndex { get; set; }
    public Polygon Geometry { get; set; } = default!;
    public double AreaSqM { get; set; }
    public double RoadFrontageMeters { get; set; }
    public bool ValidationPassed { get; set; }
    public string? ValidationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
