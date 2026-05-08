using System.Security.Claims;
using System.Text.Json;
using Geogrid.Api.Contracts;
using Geogrid.Api.Geo;
using Geogrid.Domain.Entities;
using Geogrid.Generation;
using Geogrid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Geogrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/generations")]
public class GenerationsController : ControllerBase
{
    private readonly GeogridDbContext _db;
    private readonly IPlotGenerator _generator;

    public GenerationsController(GeogridDbContext db, IPlotGenerator generator)
    {
        _db = db;
        _generator = generator;
    }

    private Guid CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }

    private Task<bool> Owns(Guid projectId)
        => _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == CurrentUserId());

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GenerationRunResponse>>> List(Guid projectId)
    {
        if (!await Owns(projectId)) return NotFound();
        var runs = await _db.GenerationRuns.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        var result = new List<GenerationRunResponse>(runs.Count);
        foreach (var r in runs) result.Add(ToResponse(r, plots: Array.Empty<Plot>()));
        return Ok(result);
    }

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult<GenerationRunResponse>> Get(Guid projectId, Guid runId)
    {
        if (!await Owns(projectId)) return NotFound();
        var run = await _db.GenerationRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId && r.ProjectId == projectId);
        if (run is null) return NotFound();
        var plots = await _db.Plots.AsNoTracking()
            .Where(p => p.GenerationRunId == runId)
            .OrderBy(p => p.BlockIndex)
            .ToListAsync();
        return Ok(ToResponse(run, plots));
    }

    /// <summary>Run the generator and persist a preview run with its plots.</summary>
    [HttpPost]
    public async Task<ActionResult<GenerationRunResponse>> Generate(Guid projectId, [FromBody] GenerationRequest req)
    {
        if (!await Owns(projectId)) return NotFound();

        var mainPlot = await _db.MainPlots.AsNoTracking().FirstOrDefaultAsync(m => m.ProjectId == projectId);
        if (mainPlot is null) return BadRequest(new { error = "Project has no main plot." });

        var roads = await _db.Roads.AsNoTracking().Where(r => r.ProjectId == projectId).ToListAsync();
        var reserved = await _db.ReservedAreas.AsNoTracking().Where(r => r.ProjectId == projectId).ToListAsync();

        var inputs = new GenerationInputs(
            mainPlot.Geometry,
            roads.Select(r => new RoadInput(r.Geometry, r.WidthMeters)).ToList(),
            reserved.Select(r => r.Geometry).ToList());

        var p = new GenerationParams(
            TargetPlotAreaSqM: req.TargetPlotAreaSqM,
            MinPlotAreaSqM: req.MinPlotAreaSqM,
            MaxPlotAreaSqM: req.MaxPlotAreaSqM,
            MinRoadFrontageMeters: req.MinRoadFrontageMeters,
            Seed: req.Seed,
            GridRotationRadians: req.GridRotationRadians);

        GenerationResult result;
        try { result = _generator.Generate(inputs, p); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }

        var stats = new GenerationStatsResponse(
            result.Stats.MainPlotAreaSqM,
            result.Stats.TotalPlotAreaSqM,
            result.Stats.TotalReservedAreaSqM,
            result.Stats.TotalRoadAreaSqM,
            result.Stats.PlotsValid,
            result.Stats.PlotsInvalid);

        var run = new GenerationRun
        {
            ProjectId = projectId,
            Status = "preview",
            Algorithm = "clipped-grid-v1",
            Seed = p.Seed,
            ParametersJson = JsonSerializer.Serialize(req),
            StatsJson = JsonSerializer.Serialize(stats),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.GenerationRuns.Add(run);

        var plotEntities = result.Plots.Select(gp => new Plot
        {
            ProjectId = projectId,
            GenerationRunId = run.Id,
            BlockIndex = gp.BlockIndex,
            Geometry = gp.GeometryWgs,
            AreaSqM = gp.AreaSqM,
            RoadFrontageMeters = gp.RoadFrontageMeters,
            ValidationPassed = gp.ValidationPassed,
            ValidationReason = gp.ValidationReason,
        }).ToList();
        _db.Plots.AddRange(plotEntities);

        await _db.SaveChangesAsync();
        return Ok(ToResponse(run, plotEntities));
    }

    /// <summary>Mark a preview as committed and discard any other previews for this project.</summary>
    [HttpPost("{runId:guid}/commit")]
    public async Task<ActionResult<GenerationRunResponse>> Commit(Guid projectId, Guid runId)
    {
        if (!await Owns(projectId)) return NotFound();
        var run = await _db.GenerationRuns.FirstOrDefaultAsync(r => r.Id == runId && r.ProjectId == projectId);
        if (run is null) return NotFound();
        if (run.Status == "discarded") return BadRequest(new { error = "Run is discarded." });

        // Discard sibling previews and any prior committed run + their plots.
        var siblings = await _db.GenerationRuns
            .Where(r => r.ProjectId == projectId && r.Id != runId)
            .ToListAsync();
        foreach (var s in siblings)
        {
            s.Status = "discarded";
        }
        var siblingIds = siblings.Select(s => s.Id).ToList();
        if (siblingIds.Count > 0)
        {
            var orphanPlots = _db.Plots.Where(p => siblingIds.Contains(p.GenerationRunId));
            _db.Plots.RemoveRange(orphanPlots);
        }

        run.Status = "committed";
        run.CommittedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var plots = await _db.Plots.AsNoTracking()
            .Where(p => p.GenerationRunId == runId)
            .OrderBy(p => p.BlockIndex)
            .ToListAsync();
        return Ok(ToResponse(run, plots));
    }

    [HttpDelete("{runId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid runId)
    {
        if (!await Owns(projectId)) return NotFound();
        var run = await _db.GenerationRuns.FirstOrDefaultAsync(r => r.Id == runId && r.ProjectId == projectId);
        if (run is null) return NotFound();
        _db.GenerationRuns.Remove(run);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static GenerationRunResponse ToResponse(GenerationRun run, IReadOnlyList<Plot> plots)
    {
        var parameters = JsonSerializer.Deserialize<GenerationRequest>(run.ParametersJson)
            ?? new GenerationRequest();
        var stats = JsonSerializer.Deserialize<GenerationStatsResponse>(run.StatsJson)
            ?? new GenerationStatsResponse(0, 0, 0, 0, 0, 0);
        return new GenerationRunResponse(
            run.Id, run.ProjectId, run.Status, run.Algorithm, run.Seed,
            parameters, stats, run.CreatedAt, run.CommittedAt,
            plots.Select(p => new PlotResponse(
                p.Id, p.GenerationRunId, p.BlockIndex,
                GeoJsonConverter.FromPolygon(p.Geometry),
                p.AreaSqM, p.RoadFrontageMeters,
                p.ValidationPassed, p.ValidationReason)).ToList());
    }
}
