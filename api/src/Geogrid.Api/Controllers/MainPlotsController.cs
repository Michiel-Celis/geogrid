using System.Security.Claims;
using Geogrid.Api.Contracts;
using Geogrid.Api.Geo;
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
    public MainPlotsController(GeogridDbContext db) => _db = db;

    private Guid CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }

    private Task<bool> UserOwnsProjectAsync(Guid projectId, Guid userId)
        => _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);

    [HttpGet]
    public async Task<ActionResult<MainPlotResponse>> Get(Guid projectId)
    {
        if (!await UserOwnsProjectAsync(projectId, CurrentUserId())) return NotFound();
        var plot = await _db.MainPlots.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == projectId);
        if (plot is null) return NoContent();
        return Ok(ToResponse(plot));
    }

    [HttpPut]
    public async Task<ActionResult<MainPlotResponse>> Put(Guid projectId, [FromBody] MainPlotRequest req)
    {
        if (!await UserOwnsProjectAsync(projectId, CurrentUserId())) return NotFound();

        Polygon polygon;
        try { polygon = GeoJsonConverter.ToPolygon(req.Geometry); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }

        if (!polygon.IsValid)
            return BadRequest(new { error = "Polygon is not valid (self-intersecting or malformed)." });

        var areaSqM = GeoJsonConverter.ApproximateAreaSqMeters(polygon);
        if (areaSqM <= 0)
            return BadRequest(new { error = "Polygon area must be positive." });
        if (areaSqM > 1_000_000_000)
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
        if (!await UserOwnsProjectAsync(projectId, CurrentUserId())) return NotFound();
        var existing = await _db.MainPlots.FirstOrDefaultAsync(p => p.ProjectId == projectId);
        if (existing is null) return NoContent();
        _db.MainPlots.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static MainPlotResponse ToResponse(MainPlot p)
        => new(p.Id, p.ProjectId, GeoJsonConverter.FromPolygon(p.Geometry), p.AreaSqM, p.CreatedAt, p.UpdatedAt);
}
