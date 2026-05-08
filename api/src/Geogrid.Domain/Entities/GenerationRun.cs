namespace Geogrid.Domain.Entities;

public class GenerationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>preview | committed | discarded</summary>
    public string Status { get; set; } = "preview";

    /// <summary>Algorithm key, e.g. "clipped-grid-v1".</summary>
    public string Algorithm { get; set; } = "clipped-grid-v1";

    public int Seed { get; set; }

    /// <summary>Serialized parameters as JSON.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>Serialized stats as JSON.</summary>
    public string StatsJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CommittedAt { get; set; }

    public List<Plot> Plots { get; set; } = new();
}
