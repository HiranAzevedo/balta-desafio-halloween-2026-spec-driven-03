using Microsoft.AspNetCore.Mvc;

namespace Encurtador.Api.Controllers;

internal static class ProblemResponses
{
    public static ObjectResult Validation(
        IReadOnlyDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(
            errors.ToDictionary(entry => entry.Key, entry => entry.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dados inválidos.",
            Detail = "Verifique os campos informados."
        };

        return CreateResult(problem, StatusCodes.Status400BadRequest);
    }

    public static ObjectResult Problem(int status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };

        return CreateResult(problem, status);
    }

    private static ObjectResult CreateResult(ProblemDetails problem, int status)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
