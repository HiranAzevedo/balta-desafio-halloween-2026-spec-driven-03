using System.Globalization;
using Encurtador.Api.Application;
using Encurtador.Api.Application.Results;
using Encurtador.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Encurtador.Api.Controllers;

[ApiController]
[Route("api/links")]
public sealed class LinksController(
    ILinkService linkService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateLinkResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLinkRequest request,
        CancellationToken cancellationToken)
    {
        var result = await linkService.CreateAsync(
            request.TargetUrl,
            request.Code,
            request.ExpiresAtUtc,
            cancellationToken);

        switch (result)
        {
            case LinkCreated created:
                var baseUrl = configuration["ShortLinks:PublicBaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl = $"{Request.Scheme}://{Request.Host}";
                }

                var response = new CreateLinkResponse(
                    $"{baseUrl.TrimEnd('/')}/{created.Code}",
                    created.Code,
                    created.ManagementKey,
                    created.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                Response.Headers.CacheControl = "no-store";
                Response.Headers.Location = $"/api/links/{created.Code}/stats";
                return StatusCode(StatusCodes.Status201Created, response);

            case LinkValidationFailed invalid:
                return ProblemResponses.Validation(invalid.Errors);

            case LinkCodeConflict:
                return ProblemResponses.Problem(
                    StatusCodes.Status409Conflict,
                    "Conflito.",
                    "O código informado já está em uso.");

            case LinkCodeGenerationFailed:
                return ProblemResponses.Problem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Serviço temporariamente indisponível.",
                    "Não foi possível gerar um código disponível. Tente novamente.");

            default:
                throw new InvalidOperationException(
                    $"Unexpected create result: {result.GetType().Name}.");
        }
    }

    [HttpGet("{code}/stats")]
    [ProducesResponseType<LinkStatsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStats(
        string code,
        [FromHeader(Name = "X-Management-Key")] string? managementKey,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await linkService.GetStatsAsync(code, managementKey, cancellationToken);

        switch (result)
        {
            case StatsAvailable stats:
                return Ok(new LinkStatsResponse(stats.AccessCount));

            case StatsCodeInvalid:
                return ProblemResponses.Validation(new Dictionary<string, string[]>
                {
                    ["code"] = ["O código deve conter de 4 a 20 caracteres válidos."]
                });

            case StatsUnauthorized:
                Response.Headers.WWWAuthenticate = "ManagementKey";
                return ProblemResponses.Problem(
                    StatusCodes.Status401Unauthorized,
                    "Não autorizado.",
                    "Chave de gerenciamento ausente ou inválida.");

            default:
                throw new InvalidOperationException(
                    $"Unexpected stats result: {result.GetType().Name}.");
        }
    }
}
