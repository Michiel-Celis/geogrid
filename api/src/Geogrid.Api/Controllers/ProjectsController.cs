using System.Security.Claims;
using Geogrid.Api.Contracts;
using Geogrid.Domain.Entities;
using Geogrid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Geogrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly GeogridDbContext _db;

    public ProjectsController(GeogridDbContext db) => _db = db;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException());

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectResponse>>> List(CancellationToken ct)
    {
        var uid = CurrentUserId;
        var items = await _db.Projects
            .Where(p => p.OwnerId == uid)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => Map(p))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken ct)
    {
        var uid = CurrentUserId;
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct);
        return p is null ? NotFound() : Ok(Map(p));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create([FromBody] CreateProjectRequest req, CancellationToken ct)
    {
        var p = new Project
        {
            OwnerId = CurrentUserId,
            Name = req.Name,
            Description = req.Description,
            Srid = req.Srid,
            CenterLat = req.CenterLat,
            CenterLon = req.CenterLon
        };
        _db.Projects.Add(p);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, Map(p));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Update(Guid id, [FromBody] UpdateProjectRequest req, CancellationToken ct)
    {
        var uid = CurrentUserId;
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct);
        if (p is null) return NotFound();

        p.Name = req.Name;
        p.Description = req.Description;
        p.Srid = req.Srid;
        p.CenterLat = req.CenterLat;
        p.CenterLon = req.CenterLon;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(Map(p));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var uid = CurrentUserId;
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct);
        if (p is null) return NotFound();
        _db.Projects.Remove(p);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ProjectResponse Map(Project p) =>
        new(p.Id, p.Name, p.Description, p.Srid, p.CenterLat, p.CenterLon, p.CreatedAt, p.UpdatedAt);
}
