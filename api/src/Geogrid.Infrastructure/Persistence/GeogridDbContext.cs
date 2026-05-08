using Geogrid.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Geogrid.Infrastructure.Persistence;

public class GeogridDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public GeogridDbContext(DbContextOptions<GeogridDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MainPlot> MainPlots => Set<MainPlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Srid).IsRequired();
            b.Property(p => p.CenterLat).IsRequired();
            b.Property(p => p.CenterLon).IsRequired();
            b.HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(p => p.OwnerId);
        });

        modelBuilder.Entity<MainPlot>(b =>
        {
            b.ToTable("main_plots");
            b.HasKey(p => p.Id);
            b.Property(p => p.Geometry)
                .HasColumnType("geometry(Polygon, 4326)")
                .IsRequired();
            b.HasOne(p => p.Project)
                .WithMany()
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(p => p.ProjectId).IsUnique();
            b.HasIndex(p => p.Geometry).HasMethod("gist");
        });
    }
}
