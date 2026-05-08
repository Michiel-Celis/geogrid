using Geogrid.Domain.Entities;

namespace Geogrid.Tests;

public class ProjectTests
{
    [Fact]
    public void NewProject_HasIdAndDefaults()
    {
        var p = new Project { Name = "Test" };
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(4326, p.Srid);
        Assert.True(p.CreatedAt <= DateTimeOffset.UtcNow);
    }
}
