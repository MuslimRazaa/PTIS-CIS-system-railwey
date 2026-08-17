using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierErp.Api.Data;
using PremierErp.Api.Models;
using System.Text.Json;

namespace PremierErp.Api.Controllers;

public record BlobDto(string Key, JsonElement Payload, DateTime? UpdatedUtc);
public record SyncPushDto(Dictionary<string, List<UpsertDto>>? Collections, List<BlobDto>? Blobs);

/// <summary>
/// Offline-first sync endpoints for premier-erp.html.
/// PUSH: client sends queued changes (collections + blobs) accumulated offline.
/// PULL: client asks for everything changed since its last sync stamp.
/// Blobs carry non-collection state: 'gl' (accounting), 'ir_library', 'ir_current'.
/// </summary>
[ApiController]
[Route("api/sync")]
[Authorize]
public class SyncController(ErpDbContext db) : ControllerBase
{
    private string Who => User.FindFirst("email")?.Value ?? "unknown";

    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] SyncPushDto dto)
    {
        if (User.IsInRole("readonly")) return Forbid();
        int records = 0, blobs = 0;

        foreach (var (collection, batch) in dto.Collections ?? new())
        {
            foreach (var item in batch)
            {
                if (string.IsNullOrWhiteSpace(item.Id)) continue;
                var row = await db.Records.FirstOrDefaultAsync(r => r.Collection == collection && r.RecordId == item.Id);
                var incoming = item.UpdatedUtc ?? DateTime.UtcNow;
                if (row is null)
                    db.Records.Add(new ErpRecord { Collection = collection, RecordId = item.Id, Payload = item.Data.GetRawText(), UpdatedUtc = incoming, UpdatedBy = Who });
                else if (incoming >= row.UpdatedUtc)
                {
                    row.Payload = item.Data.GetRawText(); row.Deleted = false;
                    row.UpdatedUtc = incoming; row.UpdatedBy = Who;
                }
                records++;
            }
        }

        foreach (var b in dto.Blobs ?? [])
        {
            var blob = await db.Blobs.FindAsync(b.Key);
            var incoming = b.UpdatedUtc ?? DateTime.UtcNow;
            if (blob is null)
                db.Blobs.Add(new SyncBlob { Key = b.Key, Payload = b.Payload.GetRawText(), UpdatedUtc = incoming, UpdatedBy = Who });
            else if (incoming >= blob.UpdatedUtc)
            {
                blob.Payload = b.Payload.GetRawText();
                blob.UpdatedUtc = incoming; blob.UpdatedBy = Who;
            }
            blobs++;
        }

        db.Audit.Add(new AuditEntry { User = Who, Action = "blob", Collection = "sync-push", RecordId = $"{records}r/{blobs}b" });
        await db.SaveChangesAsync();
        return Ok(new { ok = true, records, blobs, serverTimeUtc = DateTime.UtcNow });
    }

    [HttpGet("pull")]
    public async Task<IActionResult> Pull([FromQuery] DateTime? since)
    {
        var s = since ?? DateTime.MinValue;
        var recs = await db.Records.Where(r => r.UpdatedUtc > s).ToListAsync();
        var blobs = await db.Blobs.Where(b => b.UpdatedUtc > s).ToListAsync();
        return Ok(new
        {
            serverTimeUtc = DateTime.UtcNow,
            collections = recs.GroupBy(r => r.Collection).ToDictionary(
                g => g.Key,
                g => g.Select(r => new
                {
                    id = r.RecordId,
                    deleted = r.Deleted,
                    updatedUtc = r.UpdatedUtc,
                    data = JsonSerializer.Deserialize<JsonElement>(r.Payload)
                })),
            blobs = blobs.Select(b => new
            {
                key = b.Key,
                updatedUtc = b.UpdatedUtc,
                payload = JsonSerializer.Deserialize<JsonElement>(b.Payload)
            })
        });
    }
}
