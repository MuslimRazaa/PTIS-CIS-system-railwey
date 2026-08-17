using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierErp.Api.Data;
using PremierErp.Api.Models;
using System.Text.Json;

namespace PremierErp.Api.Controllers;

public record UpsertDto(string Id, JsonElement Data, DateTime? UpdatedUtc);

/// <summary>
/// Generic collection CRUD used by every ERP module in premier-erp.html.
/// Collection names match the front-end keys: customers, services, leads,
/// rfqs, quotes, salesorders, activities, items, stockmoves, purchaseorders,
/// invoices, assets, maintenance, employees, attendance, leave, payroll,
/// docs, audits, ncrs, risks, hse, mgtreviews, calibrations, apiq2.
/// </summary>
[ApiController]
[Route("api/records/{collection}")]
[Authorize]
public class RecordsController(ErpDbContext db) : ControllerBase
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "customers","services","leads","rfqs","quotes","salesorders","activities",
        "items","stockmoves","purchaseorders","invoices",
        "assets","maintenance",
        "employees","attendance","leave","payroll",
        "docs","audits","ncrs","risks","hse","mgtreviews","calibrations","apiq2"
    };

    private string Who => User.FindFirst("email")?.Value ?? "unknown";
    private bool ReadOnlyUser => User.IsInRole("readonly");

    [HttpGet]
    public async Task<IActionResult> List(string collection, [FromQuery] DateTime? since)
    {
        if (!Allowed.Contains(collection)) return NotFound(new { error = "Unknown collection." });
        var q = db.Records.Where(r => r.Collection == collection);
        if (since.HasValue) q = q.Where(r => r.UpdatedUtc > since.Value);
        else q = q.Where(r => !r.Deleted);
        var rows = await q.OrderBy(r => r.UpdatedUtc).ToListAsync();
        return Ok(rows.Select(r => new
        {
            id = r.RecordId,
            deleted = r.Deleted,
            updatedUtc = r.UpdatedUtc,
            data = JsonSerializer.Deserialize<JsonElement>(r.Payload)
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(string collection, [FromBody] List<UpsertDto> batch)
    {
        if (!Allowed.Contains(collection)) return NotFound(new { error = "Unknown collection." });
        if (ReadOnlyUser) return Forbid();
        int created = 0, updated = 0, skipped = 0;
        foreach (var item in batch)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) { skipped++; continue; }
            var row = await db.Records.FirstOrDefaultAsync(r => r.Collection == collection && r.RecordId == item.Id);
            var incoming = item.UpdatedUtc ?? DateTime.UtcNow;
            if (row is null)
            {
                db.Records.Add(new ErpRecord
                {
                    Collection = collection,
                    RecordId = item.Id,
                    Payload = item.Data.GetRawText(),
                    UpdatedUtc = incoming,
                    UpdatedBy = Who
                });
                created++;
            }
            else if (incoming >= row.UpdatedUtc) // last-write-wins
            {
                row.Payload = item.Data.GetRawText();
                row.Deleted = false;
                row.UpdatedUtc = incoming;
                row.UpdatedBy = Who;
                updated++;
            }
            else skipped++;
            db.Audit.Add(new AuditEntry { User = Who, Action = "upsert", Collection = collection, RecordId = item.Id });
        }
        await db.SaveChangesAsync();
        return Ok(new { created, updated, skipped });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string collection, string id)
    {
        if (!Allowed.Contains(collection)) return NotFound(new { error = "Unknown collection." });
        if (ReadOnlyUser) return Forbid();
        var row = await db.Records.FirstOrDefaultAsync(r => r.Collection == collection && r.RecordId == id);
        if (row is null) return NotFound(new { error = "Record not found." });
        row.Deleted = true;
        row.UpdatedUtc = DateTime.UtcNow;
        row.UpdatedBy = Who;
        db.Audit.Add(new AuditEntry { User = Who, Action = "delete", Collection = collection, RecordId = id });
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
