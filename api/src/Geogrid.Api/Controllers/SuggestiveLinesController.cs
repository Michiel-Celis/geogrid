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
[Route("api/projects/{projectId:guid}/suggestive-lines")]
public class SuggestiveLinesController : ControllerBase
{
    private readonly GeogridDbContext _db;
    public SuggestiveLinesController(GeogridDbContext db) => _db = db;

    private Guid CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }

    private Task<bool> Owns(Guid projectId)
        => _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == CurrentUserId());

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SuggestiveLineResponse>>> List(Guid projectId)
    {
        if (!await Owns(projectId)) return NotFound();
        var rows = await _db.SuggestiveLines.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SuggestiveLineResponse>> Get(Guid projectId, Guid id)
    {
        if (!await Owns(projectId)) return NotFound();
        var row = await _db.SuggestiveLines.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        return row is null ? NotFound() : Ok(ToResponse(row));
    }

    [HttpPost]
    public async Task<ActionResult<SuggestiveLineResponse>> Create(Guid projectId, [FromBody] SuggestiveLineRequest req)
    {
        if (!await Owns(projectId)) return NotFound();
        LineString line;
        try { line = GeoJsonConverter.ToLineString(req.Geometry); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        if (!line.IsValid) return BadRequest(new { error = "LineString is not valid." });

        var now = DateTimeOffset.UtcNow;
        var entity = new SuggestiveLine
        {
            ProjectId = projectId,
            Name = req.Name ?? string.Empty,
            Weight = req.Weight,
            ToleranceMeters = req.ToleranceMeters,
            Geometry = line,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.SuggestiveLines.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { projectId, id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SuggestiveLineResponse>> Update(Guid projectId, Guid id, [FromBody] SuggestiveLineRequest req)
    {
        if (!await Owns(projectId)) return NotFound();
        var entity = await _db.SuggestiveLines.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        if (entity is null) return NotFound();

        LineString line;
        try { line = GeoJsonConverter.ToLineString(req.Geometry); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        if (!line.IsValid) return BadRequest(new { error = "LineString is not valid." });

        entity.Name = req.Name ?? string.Empty;
        entity.Weight = req.Weight;
        entity.ToleranceMeters = req.ToleranceMeters;
        entity.Geometry = line;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id)
    {
        if (!await Owns(projectId)) return NotFound();
        var entity = await _db.SuggestiveLines.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
        if (entity is null) return NotFound();
        _db.SuggestiveLines.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SuggestiveLineResponse ToResponse(SuggestiveLine s) => new(
        s.Id, s.ProjectId, s.Name, s.Weight, s.ToleranceMeters,
        GeoJsonConverter.FromLineString(s.Geometry), s.CreatedAt, s.UpdatedAt);
}
