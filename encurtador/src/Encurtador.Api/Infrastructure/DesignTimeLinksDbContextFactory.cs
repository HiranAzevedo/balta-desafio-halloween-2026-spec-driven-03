using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Encurtador.Api.Infrastructure;

public sealed class DesignTimeLinksDbContextFactory : IDesignTimeDbContextFactory<LinksDbContext>
{
    public LinksDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LinksDbContext>()
            .UseSqlite("Data Source=links-design.db")
            .Options;

        return new LinksDbContext(options);
    }
}
