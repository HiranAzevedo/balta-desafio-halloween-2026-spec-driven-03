using Encurtador.Api.Application.Abstractions;
using Encurtador.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Encurtador.Api.Infrastructure;

public sealed class LinkRepository(LinksDbContext dbContext) : ILinkRepository
{
    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links.AnyAsync(link => link.Code == code, cancellationToken);

    public async Task<LinkInsertResult> TryAddAsync(
        Link link,
        CancellationToken cancellationToken)
    {
        dbContext.Links.Add(link);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return LinkInsertResult.Added;
        }
        catch (DbUpdateException exception)
        {
            dbContext.Entry(link).State = EntityState.Detached;

            if (exception.InnerException is SqliteException
                {
                    SqliteExtendedErrorCode: 2067
                } sqliteException)
            {
                if (sqliteException.Message.Contains("Links.Code", StringComparison.Ordinal))
                {
                    return LinkInsertResult.CodeConflict;
                }

                if (sqliteException.Message.Contains(
                    "Links.ManagementKeyHash",
                    StringComparison.Ordinal))
                {
                    return LinkInsertResult.ManagementKeyConflict;
                }
            }

            throw;
        }
    }

    public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Code == code, cancellationToken);

    public Task<int> TryRegisterAccessAsync(
        string code,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        dbContext.Links
            .Where(link => link.Code == code &&
                (link.ExpiresAtUtc == null || link.ExpiresAtUtc > nowUtc))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => link.AccessCount,
                    link => link.AccessCount + 1),
                cancellationToken);
}
