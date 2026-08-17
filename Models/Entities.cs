using System.ComponentModel.DataAnnotations;

namespace PremierErp.Api.Models;

/// <summary>Application user with role-based access.</summary>
public class AppUser
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(160)] public string Email { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    [MaxLength(40)] public string Role { get; set; } = "user"; // admin | manager | user | readonly
    public bool Active { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Generic ERP record. One row per record in any front-end collection
/// (customers, services, leads, rfqs, quotes, salesorders, activities, items,
/// stockmoves, purchaseorders, invoices, assets, maintenance, employees,
/// attendance, leave, payroll, docs, audits, ncrs, risks, hse, mgtreviews,
/// calibrations, apiq2 ...). Payload is the record JSON exactly as the
/// HTML client stores it, so client and server never disagree on shape.
/// </summary>
public class ErpRecord
{
    [Key] public long RowId { get; set; }
    [MaxLength(60)] public string Collection { get; set; } = "";
    [MaxLength(60)] public string RecordId { get; set; } = "";   // client-generated id (uid())
    public string Payload { get; set; } = "{}";                  // JSON
    public bool Deleted { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(160)] public string UpdatedBy { get; set; } = "";
}

/// <summary>
/// Named JSON blob for non-collection state: 'gl' (multi-company accounting),
/// 'ir_library' (saved inspection reports), 'ir_current', settings, etc.
/// </summary>
public class SyncBlob
{
    [Key][MaxLength(80)] public string Key { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(160)] public string UpdatedBy { get; set; } = "";
}

/// <summary>Audit trail of every write.</summary>
public class AuditEntry
{
    [Key] public long Id { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(160)] public string User { get; set; } = "";
    [MaxLength(30)] public string Action { get; set; } = "";     // upsert | delete | blob | login
    [MaxLength(60)] public string Collection { get; set; } = "";
    [MaxLength(60)] public string RecordId { get; set; } = "";
}
