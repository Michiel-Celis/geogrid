using NetTopologySuite.Geometries;

namespace Geogrid.Domain.Entities;

public class Road
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = "local"; // arterial / collector / local / alley
    public int Lanes { get; set; } = 2;
    public double WidthMeters { get; set; } = 6.0;
    public bool HasFootpath { get; set; }
    public bool HasBikepath { get; set; }
    public LineString Geometry { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
