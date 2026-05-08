using NetTopologySuite.Geometries;

namespace Geogrid.Domain.Entities;

public class MainPlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Polygon Geometry { get; set; } = default!;
    public double AreaSqM { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
