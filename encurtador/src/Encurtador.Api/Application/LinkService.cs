using Encurtador.Api.Application.Abstractions;
using Encurtador.Api.Application.Results;
using Encurtador.Api.Domain;

namespace Encurtador.Api.Application;

public sealed class LinkService(
    ILinkRepository repository,
    TimeProvider timeProvider) : ILinkService
{
    private static readonly byte[] MissingCodeHash = new byte[32];

    public async Task<CreateLinkResult> CreateAsync(
        string? targetUrl,
        string? customCode,
        string? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            errors["targetUrl"] = ["A URL de destino é obrigatória."];
        }
        else if (targetUrl.Length > 2048)
        {
            errors["targetUrl"] = ["A URL de destino deve ter no máximo 2048 caracteres."];
        }
        else if (!TargetUrlRules.IsValid(targetUrl))
        {
            errors["targetUrl"] = ["A URL de destino deve ser um endereço http ou https válido."];
        }

        if (customCode is not null && !LinkCodeRules.IsValidCode(customCode))
        {
            errors["code"] =
            [
                "O código deve ter de 4 a 20 caracteres, usando apenas letras maiúsculas de A a Z e números de 1 a 9."
            ];
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!ExpirationRules.TryParse(
            expiresAtUtc,
            nowUtc,
            out var expirationDate,
            out var exclusiveExpirationUtc,
            out var expirationError))
        {
            errors["expiresAtUtc"] = expirationError switch
            {
                ExpirationError.PastDate => ["A data de expiração não pode estar no passado."],
                ExpirationError.OutOfRange =>
                    ["A data de expiração não pode ultrapassar o último dia representável."],
                _ => ["A data de expiração deve estar no formato AAAA-MM-DD."]
            };
        }

        if (errors.Count > 0)
        {
            return new LinkValidationFailed(errors);
        }

        var isCustomCode = customCode is not null;
        var codeAttempts = isCustomCode ? 1 : 5;

        for (var attempt = 0; attempt < codeAttempts; attempt++)
        {
            var code = customCode ?? CodeGenerator.Generate();
            if (await repository.CodeExistsAsync(code, cancellationToken))
            {
                if (isCustomCode)
                {
                    return new LinkCodeConflict();
                }

                continue;
            }

            while (true)
            {
                var managementKey = ManagementKeyHasher.Generate();
                var link = new Link(
                    code,
                    targetUrl!,
                    isCustomCode,
                    nowUtc,
                    exclusiveExpirationUtc,
                    managementKey.Hash);

                var insertion = await repository.TryAddAsync(link, cancellationToken);
                switch (insertion)
                {
                    case LinkInsertResult.Added:
                        return new LinkCreated(code, managementKey.Value, expirationDate);
                    case LinkInsertResult.ManagementKeyConflict:
                        continue;
                    case LinkInsertResult.CodeConflict when isCustomCode:
                        return new LinkCodeConflict();
                    case LinkInsertResult.CodeConflict:
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected repository insert result: {insertion}.");
                }

                break;
            }
        }

        return isCustomCode
            ? new LinkCodeConflict()
            : new LinkCodeGenerationFailed();
    }

    public async Task<RedirectResult> AccessAsync(
        string code,
        CancellationToken cancellationToken)
    {
        if (!LinkCodeRules.IsValidCode(code))
        {
            return new LinkCodeInvalid();
        }

        var link = await repository.FindByCodeAsync(code, cancellationToken);
        if (link is null)
        {
            return new LinkNotFound();
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (IsExpired(link, nowUtc))
        {
            return new LinkExpired();
        }

        if (await repository.TryRegisterAccessAsync(code, nowUtc, cancellationToken) == 1)
        {
            return new LinkRedirectReady(link.TargetUrl);
        }

        var currentLink = await repository.FindByCodeAsync(code, cancellationToken);
        if (currentLink is null)
        {
            return new LinkNotFound();
        }

        if (IsExpired(currentLink, nowUtc))
        {
            return new LinkExpired();
        }

        throw new InvalidOperationException(
            "The repository did not register an access for a valid link.");
    }

    public async Task<StatsResult> GetStatsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken)
    {
        if (!LinkCodeRules.IsValidCode(code))
        {
            return new StatsCodeInvalid();
        }

        var link = await repository.FindByCodeAsync(code, cancellationToken);
        var expectedHash = link?.ManagementKeyHash ?? MissingCodeHash;
        var authorized = ManagementKeyHasher.Verify(managementKey, expectedHash);

        if (link is null || !authorized)
        {
            return new StatsUnauthorized();
        }

        return new StatsAvailable(link.AccessCount);
    }

    private static bool IsExpired(Link link, DateTime nowUtc) =>
        link.ExpiresAtUtc is { } expiresAtUtc && nowUtc >= expiresAtUtc;
}
