using Microsoft.EntityFrameworkCore;
using PremierErp.Api.Models;

namespace PremierErp.Api.Data;

public class ErpDbContext(DbContextOptions<ErpDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ErpRecord> Records => Set<ErpRecord>();
    public DbSet<SyncBlob> Blobs => Set<SyncBlob>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        mb.Entity<ErpRecord>().HasIndex(r => new { r.Collection, r.RecordId }).IsUnique();
        mb.Entity<ErpRecord>().HasIndex(r => new { r.Collection, r.UpdatedUtc });
        mb.Entity<SyncBlob>().HasIndex(b => b.UpdatedUtc);
    }
}
