using Geogrid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Geogrid.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly GeogridDbContext _db;

    public HealthController(GeogridDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dbOk = await _db.Database.CanConnectAsync(ct);
        return Ok(new
        {
            status = dbOk ? "healthy" : "degraded",
            database = dbOk ? "up" : "down",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
