using Geogrid.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Geogrid.Infrastructure.Persistence;

public class GeogridDbContext : DbContext
{
    public GeogridDbContext(DbContextOptions<GeogridDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Srid).IsRequired();
            b.Property(p => p.CreatedAt).IsRequired();
        });
    }
}
