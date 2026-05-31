using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<FileRecord> FileRecords => Set<FileRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<FileRecord>(entity =>
        {
            entity.ToTable("file_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("uuid");
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.FileType).IsRequired();
            entity.Property(e => e.FlopDiskNumber);
            entity.Property(e => e.Date).HasColumnType("timestamptz");
            entity.Property(e => e.Client).IsRequired();
            entity.Property(e => e.FileNumber);
        });
    }
}
