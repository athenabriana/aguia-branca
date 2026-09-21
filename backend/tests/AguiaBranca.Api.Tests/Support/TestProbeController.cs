using System.ComponentModel.DataAnnotations;
using AguiaBranca.Api.Controllers;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AguiaBranca.Api.Tests.Support;

// Records: a validação (DataAnnotations) vai no PARÂMETRO do construtor, não na propriedade.
public sealed record EchoRequest([Required, StringLength(5)] string Name, IdeaStatus? Status);

/// <summary>Endpoints de teste (só existem nos testes): exercitam o pipeline de erros/serialização.</summary>
public sealed class TestProbeController(ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet("boom")] public IActionResult Boom() => throw new InvalidOperationException("segredo-interno-123");

    [HttpGet("domain/{code}")]
    public IActionResult Domain(string code) => throw new DomainException(code, "regra violada");

    [HttpGet("validation")]
    public IActionResult Validation() => throw new FluentValidation.ValidationException(
        [new ValidationFailure("Ice.Impact", "Impacto inválido."), new ValidationFailure("Title", "Título obrigatório.")]);

    [HttpGet("duplicate")] public IActionResult Duplicate() => throw new DuplicateKeyException("ux_x");
    [HttpGet("concurrency")] public IActionResult Concurrency() => throw new ConcurrencyConflictException();
    [HttpGet("unauthorized-access")] public IActionResult Unauthorized_() => throw new UnauthorizedAccessException();
    [HttpGet("payload-too-large")] public IActionResult TooLarge() => throw new BadHttpRequestException("grande", 413);
    [HttpGet("cancel")] public IActionResult Cancel() => throw new OperationCanceledException(HttpContext.RequestAborted);

    [HttpPost("echo")] public IActionResult Echo([FromBody] EchoRequest request) => Ok(request);

    [HttpGet("items/{id:objectid}")] public IActionResult Item(string id) => Ok(new { id });

    [HttpGet("result/{kind}")]
    public IActionResult ResultKind(string kind) => kind switch
    {
        "ok" => FromResult(Result.Ok(new { value = 1 })),
        "notfound" => FromResult(Result.Fail<int>(Error.NotFound("Ideia não encontrada."))),
        "forbidden" => FromResult(Result.Fail<int>(Error.Forbidden("Sem permissão.", "SELF_APPROVAL_FORBIDDEN"))),
        "conflict" => FromResult(Result.Fail<int>(Error.Conflict("IDEA_NOT_EDITABLE", "Não editável."))),
        "unprocessable" => FromResult(Result.Fail<int>(Error.Unprocessable("GUIDELINE_NOT_FOUND", "Orientação inexistente.", "guidelineId"))),
        "ai" => FromResult(Result.Fail<int>(Error.External("AI_UNAVAILABLE", "IA indisponível."))),
        "multi" => FromResult(Result.Fail<int>([Error.Validation("A inválido", "a"), Error.Validation("B inválido", "b")])),
        _ => NoContentFrom(Result.Ok(1))
    };

    [HttpGet("shapes")]
    public IActionResult Shapes() => Ok(new
    {
        Status = IdeaStatus.EM_ANALISE,
        Division = Division.LOGISTICA,
        OptionalNote = (string?)null,
        CreatedAt = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc),
        Investment = 1234.5m
    });

    [HttpGet("me")]
    public IActionResult Me() => Ok(new { currentUser.IsAuthenticated, currentUser.Id });
}
