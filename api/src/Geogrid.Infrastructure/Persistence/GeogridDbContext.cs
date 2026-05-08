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
    public DbSet<Road> Roads => Set<Road>();
    public DbSet<ReservedArea> ReservedAreas => Set<ReservedArea>();
    public DbSet<SuggestiveLine> SuggestiveLines => Set<SuggestiveLine>();
    public DbSet<GenerationRun> GenerationRuns => Set<GenerationRun>();
    public DbSet<Plot> Plots => Set<Plot>();

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

        modelBuilder.Entity<Road>(b =>
        {
            b.ToTable("roads");
            b.HasKey(r => r.Id);
            b.Property(r => r.Name).HasMaxLength(200);
            b.Property(r => r.Class).HasMaxLength(40).IsRequired();
            b.Property(r => r.Geometry)
                .HasColumnType("geometry(LineString, 4326)")
                .IsRequired();
            b.HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(r => r.ProjectId);
            b.HasIndex(r => r.Geometry).HasMethod("gist");
        });

        modelBuilder.Entity<ReservedArea>(b =>
        {
            b.ToTable("reserved_areas");
            b.HasKey(r => r.Id);
            b.Property(r => r.Name).HasMaxLength(200);
            b.Property(r => r.Kind).HasMaxLength(40).IsRequired();
            b.Property(r => r.Geometry)
                .HasColumnType("geometry(Polygon, 4326)")
                .IsRequired();
            b.HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(r => r.ProjectId);
            b.HasIndex(r => r.Geometry).HasMethod("gist");
        });

        modelBuilder.Entity<SuggestiveLine>(b =>
        {
            b.ToTable("suggestive_lines");
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).HasMaxLength(200);
            b.Property(s => s.Geometry)
                .HasColumnType("geometry(LineString, 4326)")
                .IsRequired();
            b.HasOne(s => s.Project)
                .WithMany()
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(s => s.ProjectId);
            b.HasIndex(s => s.Geometry).HasMethod("gist");
        });

        modelBuilder.Entity<GenerationRun>(b =>
        {
            b.ToTable("generation_runs");
            b.HasKey(g => g.Id);
            b.Property(g => g.Status).HasMaxLength(20).IsRequired();
            b.Property(g => g.Algorithm).HasMaxLength(80).IsRequired();
            b.Property(g => g.ParametersJson).HasColumnType("jsonb").IsRequired();
            b.Property(g => g.StatsJson).HasColumnType("jsonb").IsRequired();
            b.HasOne(g => g.Project)
                .WithMany()
                .HasForeignKey(g => g.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(g => g.ProjectId);
            b.HasIndex(g => new { g.ProjectId, g.Status });
        });

        modelBuilder.Entity<Plot>(b =>
        {
            b.ToTable("plots");
            b.HasKey(p => p.Id);
            b.Property(p => p.Geometry)
                .HasColumnType("geometry(Polygon, 4326)")
                .IsRequired();
            b.Property(p => p.ValidationReason).HasMaxLength(500);
            b.HasOne(p => p.Project)
                .WithMany()
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(p => p.GenerationRun)
                .WithMany(g => g.Plots)
                .HasForeignKey(p => p.GenerationRunId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(p => p.ProjectId);
            b.HasIndex(p => p.GenerationRunId);
            b.HasIndex(p => p.Geometry).HasMethod("gist");
        });
    }
}
