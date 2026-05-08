using System.Security.Claims;
using Geogrid.Api.Contracts;
using Geogrid.Domain.Entities;
using Geogrid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Geogrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/main-plot")]
public class MainPlotsController : ControllerBase
{
    private readonly GeogridDbContext _db;
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public MainPlotsController(GeogridDbContext db) => _db = db;

    private Guid CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }

    private async Task<bool> UserOwnsProjectAsync(Guid projectId, Guid userId)
        => await _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);

    [HttpGet]
    public async Task<ActionResult<MainPlotResponse>> Get(Guid projectId)
    {
        var userId = CurrentUserId();
        if (!await UserOwnsProjectAsync(projectId, userId)) return NotFound();

        var plot = await _db.MainPlots.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId);
        if (plot is null) return NoContent();

        return Ok(ToResponse(plot));
    }

    [HttpPut]
    public async Task<ActionResult<MainPlotResponse>> Put(Guid projectId, [FromBody] MainPlotRequest req)
    {
        var userId = CurrentUserId();
        if (!await UserOwnsProjectAsync(projectId, userId)) return NotFound();

        Polygon polygon;
        try
        {
            polygon = ToPolygon(req.Geometry);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        if (!polygon.IsValid)
            return BadRequest(new { error = "Polygon is not valid (self-intersecting or malformed)." });

        // Geodesic area in m² via NTS spherical approximation using lon/lat is unreliable;
        // compute approximate area via projection to local UTM-like equirectangular at centroid.
        var areaSqM = ApproximateAreaSqMeters(polygon);
        if (areaSqM <= 0)
            return BadRequest(new { error = "Polygon area must be positive." });
        if (areaSqM > 1_000_000_000) // 1000 km² sanity cap
            return BadRequest(new { error = "Polygon area exceeds sanity limit (1000 km²)." });

        var existing = await _db.MainPlots.FirstOrDefaultAsync(p => p.ProjectId == projectId);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            existing = new MainPlot
            {
                ProjectId = projectId,
                Geometry = polygon,
                AreaSqM = areaSqM,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.MainPlots.Add(existing);
        }
        else
        {
            existing.Geometry = polygon;
            existing.AreaSqM = areaSqM;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        return Ok(ToResponse(existing));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(Guid projectId)
    {
        var userId = CurrentUserId();
        if (!await UserOwnsProjectAsync(projectId, userId)) return NotFound();

        var existing = await _db.MainPlots.FirstOrDefaultAsync(p => p.ProjectId == projectId);
        if (existing is null) return NoContent();

        _db.MainPlots.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static Polygon ToPolygon(GeoJsonPolygon g)
    {
        if (!string.Equals(g.Type, "Polygon", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Geometry type must be 'Polygon'.");
        if (g.Coordinates is null || g.Coordinates.Length == 0)
            throw new ArgumentException("Polygon coordinates required.");

        var rings = new LinearRing[g.Coordinates.Length];
        for (var r = 0; r < g.Coordinates.Length; r++)
        {
            var ringCoords = g.Coordinates[r];
            if (ringCoords.Length < 4)
                throw new ArgumentException("Each ring must have at least 4 positions (closed).");
            var coords = new Coordinate[ringCoords.Length];
            for (var i = 0; i < ringCoords.Length; i++)
            {
                var pos = ringCoords[i];
                if (pos.Length < 2) throw new ArgumentException("Each position needs [lon, lat].");
                coords[i] = new Coordinate(pos[0], pos[1]);
            }
            if (!coords[0].Equals2D(coords[^1]))
                throw new ArgumentException("Ring must be closed (first == last).");
            rings[r] = GeometryFactory.CreateLinearRing(coords);
        }

        var shell = rings[0];
        var holes = rings.Length > 1 ? rings[1..] : Array.Empty<LinearRing>();
        var polygon = GeometryFactory.CreatePolygon(shell, holes);
        polygon.SRID = 4326;
        return polygon;
    }

    private static MainPlotResponse ToResponse(MainPlot p)
        => new(p.Id, p.ProjectId, ToGeoJson(p.Geometry), p.AreaSqM, p.CreatedAt, p.UpdatedAt);

    private static GeoJsonPolygon ToGeoJson(Polygon polygon)
    {
        var rings = new List<double[][]>(1 + polygon.NumInteriorRings)
        {
            RingToArray(polygon.ExteriorRing.Coordinates),
        };
        for (var i = 0; i < polygon.NumInteriorRings; i++)
            rings.Add(RingToArray(polygon.GetInteriorRingN(i).Coordinates));
        return new GeoJsonPolygon("Polygon", rings.ToArray());
    }

    private static double[][] RingToArray(Coordinate[] coords)
    {
        var arr = new double[coords.Length][];
        for (var i = 0; i < coords.Length; i++)
            arr[i] = new[] { coords[i].X, coords[i].Y };
        return arr;
    }

    private static double ApproximateAreaSqMeters(Polygon polygon)
    {
        // Equirectangular projection at polygon centroid latitude.
        var centroid = polygon.Centroid;
        var lat0 = centroid.Y * Math.PI / 180.0;
        const double R = 6_378_137.0;
        var cosLat = Math.Cos(lat0);

        Coordinate[] Project(Coordinate[] cs)
        {
            var p = new Coordinate[cs.Length];
            for (var i = 0; i < cs.Length; i++)
            {
                var x = R * (cs[i].X * Math.PI / 180.0) * cosLat;
                var y = R * (cs[i].Y * Math.PI / 180.0);
                p[i] = new Coordinate(x, y);
            }
            return p;
        }

        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
        var shell = factory.CreateLinearRing(Project(polygon.ExteriorRing.Coordinates));
        var holes = new LinearRing[polygon.NumInteriorRings];
        for (var i = 0; i < polygon.NumInteriorRings; i++)
            holes[i] = factory.CreateLinearRing(Project(polygon.GetInteriorRingN(i).Coordinates));
        return factory.CreatePolygon(shell, holes).Area;
    }
}
