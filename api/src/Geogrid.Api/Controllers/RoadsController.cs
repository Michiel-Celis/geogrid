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
[Route("api/projects/{projectId:guid}/roads")]
public class RoadsController : ControllerBase
{
    private readonly GeogridDbContext _db;
    public RoadsController(GeogridDbContext db) => _db = db;

    private Guid CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }

    private Task<bool> Owns(Guid projectId)
        => _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == CurrentUserId());

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoadResponse>>> List(Guid projectId)
    {
        if (!await Owns(projectId)) return NotFound();
        var rows = await _db.Roads.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoadResponse>> Get(Guid projectId, Guid id)
    {
        if (!await Owns(projectId)) return NotFound();
        var row = await _db.Roads.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        return row is null ? NotFound() : Ok(ToResponse(row));
    }

    [HttpPost]
    public async Task<ActionResult<RoadResponse>> Create(Guid projectId, [FromBody] RoadRequest req)
    {
        if (!await Owns(projectId)) return NotFound();
        LineString line;
        try { line = GeoJsonConverter.ToLineString(req.Geometry); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        if (!line.IsValid) return BadRequest(new { error = "LineString is not valid." });

        var now = DateTimeOffset.UtcNow;
        var entity = new Road
        {
            ProjectId = projectId,
            Name = req.Name ?? string.Empty,
            Class = req.Class,
            Lanes = req.Lanes,
            WidthMeters = req.WidthMeters,
            HasFootpath = req.HasFootpath,
            HasBikepath = req.HasBikepath,
            Geometry = line,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Roads.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { projectId, id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoadResponse>> Update(Guid projectId, Guid id, [FromBody] RoadRequest req)
    {
        if (!await Owns(projectId)) return NotFound();
        var entity = await _db.Roads.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        if (entity is null) return NotFound();

        LineString line;
        try { line = GeoJsonConverter.ToLineString(req.Geometry); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        if (!line.IsValid) return BadRequest(new { error = "LineString is not valid." });

        entity.Name = req.Name ?? string.Empty;
        entity.Class = req.Class;
        entity.Lanes = req.Lanes;
        entity.WidthMeters = req.WidthMeters;
        entity.HasFootpath = req.HasFootpath;
        entity.HasBikepath = req.HasBikepath;
        entity.Geometry = line;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id)
    {
        if (!await Owns(projectId)) return NotFound();
        var entity = await _db.Roads.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        if (entity is null) return NotFound();
        _db.Roads.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static RoadResponse ToResponse(Road r) => new(
        r.Id, r.ProjectId, r.Name, r.Class, r.Lanes, r.WidthMeters,
        r.HasFootpath, r.HasBikepath, GeoJsonConverter.FromLineString(r.Geometry),
        r.CreatedAt, r.UpdatedAt);
}
