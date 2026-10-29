using Encurtador.Api.Application.Abstractions;
using Encurtador.Api.Application;
using Encurtador.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var fields = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry => entry.Key.StartsWith("$", StringComparison.Ordinal)
                    ? "request"
                    : entry.Key)
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    field => field,
                    _ => new[] { "O valor informado é inválido." },
                    StringComparer.Ordinal);

            if (fields.Count == 0)
            {
                fields["request"] = ["O conteúdo enviado é inválido."];
            }

            var problem = new ValidationProblemDetails(fields)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos.",
                Detail = "Verifique os campos informados."
            };

            var result = new BadRequestObjectResult(problem);
            result.ContentTypes.Add("application/problem+json");
            return result;
        };
    });

var connectionString = builder.Configuration.GetConnectionString("Links")
    ?? throw new InvalidOperationException("ConnectionStrings:Links não foi configurada.");

builder.Services.AddDbContext<LinksDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ILinkRepository, LinkRepository>();
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LinksDbContext>();
    dbContext.Database.OpenConnection();
    dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    dbContext.Database.Migrate();
}

app.MapControllers();
app.Run();

public partial class Program { }
