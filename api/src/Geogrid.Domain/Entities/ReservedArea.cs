using NetTopologySuite.Geometries;

namespace Geogrid.Domain.Entities;

public class ReservedArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "park"; // town_square / forest / park / pond
    public Polygon Geometry { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
