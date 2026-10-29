using Encurtador.Api.Application;
using Encurtador.Api.Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace Encurtador.Api.Controllers;

[ApiController]
[Route("")]
public sealed class RedirectController(ILinkService linkService) : ControllerBase
{
    [HttpGet("{code}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> RedirectToTarget(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await linkService.AccessAsync(code, cancellationToken);

        return result switch
        {
            LinkRedirectReady redirect => Redirect(redirect.TargetUrl),
            LinkCodeInvalid => ProblemResponses.Validation(new Dictionary<string, string[]>
            {
                ["code"] = ["O código deve conter de 4 a 20 caracteres válidos."]
            }),
            LinkNotFound => ProblemResponses.Problem(
                StatusCodes.Status404NotFound,
                "Link não encontrado.",
                "Link não encontrado."),
            LinkExpired => ProblemResponses.Problem(
                StatusCodes.Status410Gone,
                "Link expirado.",
                "Este link expirou."),
            _ => throw new InvalidOperationException(
                $"Unexpected redirect result: {result.GetType().Name}.")
        };
    }
}
