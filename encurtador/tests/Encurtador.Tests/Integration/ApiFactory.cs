using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Encurtador.Api.Infrastructure;

namespace Encurtador.Tests.Integration;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly bool ownsDatabase;
    private readonly TimeProvider timeProvider;

    public ApiFactory(string? databasePath = null, TimeProvider? timeProvider = null)
    {
        ownsDatabase = databasePath is null;
        DatabasePath = databasePath
            ?? Path.Combine(Path.GetTempPath(), $"encurtador-{Guid.NewGuid():N}.db");
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string DatabasePath { get; }

    public TestLogProvider LogProvider { get; } = new();

    public new HttpClient CreateClient() =>
        base.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString =
            $"Data Source={DatabasePath};Pooling=False;Default Timeout=30";

        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(LogProvider));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LinksDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LinksDbContext>>();
            services.RemoveAll<LinksDbContext>();
            services.AddDbContext<LinksDbContext>(options => options.UseSqlite(connectionString));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(timeProvider);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && ownsDatabase)
        {
            DeleteDatabaseFiles();
        }
    }

    public void DeleteDatabaseFiles()
    {
        foreach (var path in new[] { DatabasePath, $"{DatabasePath}-shm", $"{DatabasePath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public sealed class TestLogProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> entries = new();

    public IReadOnlyCollection<string> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new TestLogger(entries);

    public void Dispose()
    {
    }

    private sealed class TestLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(formatter(state, exception));
        }
    }
}
