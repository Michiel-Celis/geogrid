using NetTopologySuite.Geometries;

namespace Geogrid.Domain.Entities;

public class SuggestiveLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;       // attraction strength for generation
    public double ToleranceMeters { get; set; } = 25.0; // how loosely roads may follow it
    public LineString Geometry { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
