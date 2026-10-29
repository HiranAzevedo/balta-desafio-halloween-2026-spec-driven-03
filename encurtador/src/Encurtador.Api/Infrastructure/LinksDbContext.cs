using Encurtador.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Encurtador.Api.Infrastructure;

public sealed class LinksDbContext(DbContextOptions<LinksDbContext> options) : DbContext(options)
{
    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<Link>();
        link.ToTable("Links", table =>
        {
            table.HasCheckConstraint("CK_Links_Code_Length", "length(Code) BETWEEN 4 AND 20");
            table.HasCheckConstraint("CK_Links_TargetUrl_Length", "length(TargetUrl) <= 2048");
            table.HasCheckConstraint("CK_Links_AccessCount_NonNegative", "AccessCount >= 0");
            table.HasCheckConstraint("CK_Links_ManagementKeyHash_Length", "length(ManagementKeyHash) = 32");
        });

        link.HasKey(item => item.Id);
        link.Property(item => item.Code).IsRequired();
        link.Property(item => item.TargetUrl).IsRequired().HasMaxLength(2048);
        link.Property(item => item.IsCustomCode).IsRequired();
        link.Property(item => item.CreatedAtUtc).IsRequired();
        link.Property(item => item.ExpiresAtUtc);
        link.Property(item => item.AccessCount).IsRequired().HasDefaultValue(0L);
        link.Property(item => item.ManagementKeyHash).IsRequired().HasMaxLength(32);
        link.HasIndex(item => item.Code).IsUnique().HasDatabaseName("IX_Links_Code");
        link.HasIndex(item => item.ManagementKeyHash)
            .IsUnique()
            .HasDatabaseName("IX_Links_ManagementKeyHash");
    }
}
