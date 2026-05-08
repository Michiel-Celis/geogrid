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
[Route("api/projects/{projectId:guid}/reserved-areas")]
public class ReservedAreasController : ControllerBase
{
    private readonly GeogridDbContext _db;
    public ReservedAreasController(GeogridDbContext db) => _db = db;

    private Guid CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }

    private Task<bool> Owns(Guid projectId)
        => _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == CurrentUserId());

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReservedAreaResponse>>> List(Guid projectId)
    {
        if (!await Owns(projectId)) return NotFound();
        var rows = await _db.ReservedAreas.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReservedAreaResponse>> Get(Guid projectId, Guid id)
    {
        if (!await Owns(projectId)) return NotFound();
        var row = await _db.ReservedAreas.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        return row is null ? NotFound() : Ok(ToResponse(row));
    }

    [HttpPost]
    public async Task<ActionResult<ReservedAreaResponse>> Create(Guid projectId, [FromBody] ReservedAreaRequest req)
    {
        if (!await Owns(projectId)) return NotFound();
        Polygon polygon;
        try { polygon = GeoJsonConverter.ToPolygon(req.Geometry); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        if (!polygon.IsValid) return BadRequest(new { error = "Polygon is not valid." });

        var now = DateTimeOffset.UtcNow;
        var entity = new ReservedArea
        {
            ProjectId = projectId,
            Name = req.Name ?? string.Empty,
            Kind = req.Kind,
            Geometry = polygon,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.ReservedAreas.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { projectId, id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReservedAreaResponse>> Update(Guid projectId, Guid id, [FromBody] ReservedAreaRequest req)
    {
        if (!await Owns(projectId)) return NotFound();
        var entity = await _db.ReservedAreas.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        if (entity is null) return NotFound();

        Polygon polygon;
        try { polygon = GeoJsonConverter.ToPolygon(req.Geometry); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        if (!polygon.IsValid) return BadRequest(new { error = "Polygon is not valid." });

        entity.Name = req.Name ?? string.Empty;
        entity.Kind = req.Kind;
        entity.Geometry = polygon;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id)
    {
        if (!await Owns(projectId)) return NotFound();
        var entity = await _db.ReservedAreas.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        if (entity is null) return NotFound();
        _db.ReservedAreas.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ReservedAreaResponse ToResponse(ReservedArea r) => new(
        r.Id, r.ProjectId, r.Name, r.Kind, GeoJsonConverter.FromPolygon(r.Geometry),
        r.CreatedAt, r.UpdatedAt);
}
