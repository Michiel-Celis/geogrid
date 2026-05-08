namespace Geogrid.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>EPSG SRID used for canonical/metric calculations (e.g. UTM zone).</summary>
    public int Srid { get; set; } = 4326;

    /// <summary>Map-centering latitude (WGS84).</summary>
    public double CenterLat { get; set; }

    /// <summary>Map-centering longitude (WGS84).</summary>
    public double CenterLon { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
