using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierErp.Api.Data;
using System.Text.Json;

namespace PremierErp.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(ErpDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var counts = await db.Records.Where(r => !r.Deleted)
            .GroupBy(r => r.Collection)
            .Select(g => new { collection = g.Key, count = g.Count() })
            .ToListAsync();

        // Open NCRs / overdue calibrations computed server-side from payload JSON
        var ncrs = await db.Records.Where(r => r.Collection == "ncrs" && !r.Deleted).Select(r => r.Payload).ToListAsync();
        int openNcrs = ncrs.Count(p => { try { return JsonDocument.Parse(p).RootElement.TryGetProperty("status", out var s) && s.GetString() != "Closed"; } catch { return false; } });

        var cals = await db.Records.Where(r => r.Collection == "calibrations" && !r.Deleted).Select(r => r.Payload).ToListAsync();
        int overdueCal = cals.Count(p => { try { return JsonDocument.Parse(p).RootElement.TryGetProperty("status", out var s) && s.GetString() == "Overdue"; } catch { return false; } });

        return Ok(new { counts, openNcrs, overdueCal, serverTimeUtc = DateTime.UtcNow });
    }
}
