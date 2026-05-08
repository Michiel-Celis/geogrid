namespace Geogrid.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Srid { get; set; } = 4326;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
