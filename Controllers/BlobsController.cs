using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremierErp.Api.Data;
using PremierErp.Api.Models;
using System.Text.Json;

namespace PremierErp.Api.Controllers;

/// <summary>Direct blob access: 'gl' accounting state, 'ir_library', settings.</summary>
[ApiController]
[Route("api/blobs")]
[Authorize]
public class BlobsController(ErpDbContext db) : ControllerBase
{
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var b = await db.Blobs.FindAsync(key);
        if (b is null) return NotFound(new { error = "Blob not found." });
        return Ok(new { key = b.Key, updatedUtc = b.UpdatedUtc, payload = JsonSerializer.Deserialize<JsonElement>(b.Payload) });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Put(string key, [FromBody] JsonElement payload)
    {
        if (User.IsInRole("readonly")) return Forbid();
        var who = User.FindFirst("email")?.Value ?? "unknown";
        var b = await db.Blobs.FindAsync(key);
        if (b is null) db.Blobs.Add(new SyncBlob { Key = key, Payload = payload.GetRawText(), UpdatedBy = who });
        else { b.Payload = payload.GetRawText(); b.UpdatedUtc = DateTime.UtcNow; b.UpdatedBy = who; }
        db.Audit.Add(new AuditEntry { User = who, Action = "blob", Collection = key });
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
