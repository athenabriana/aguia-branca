using AguiaBranca.Api.Http;
using AguiaBranca.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace AguiaBranca.Api.Controllers;

/// <summary>Base dos controllers: rota versionada e tradução <c>Result → HTTP</c> (controllers finos).</summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ProblemFrom(IReadOnlyList<Error> errors)
    {
        var problem = ApiProblems.FromErrors(HttpContext, errors);
        if (errors.Select(e => e.RetryAfterSeconds).FirstOrDefault(r => r is not null) is { } retryAfter)
            Response.Headers.RetryAfter = retryAfter.ToString();
        return new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { ApiProblems.ContentType } };
    }

    /// <summary>Sucesso → <paramref name="onSuccess"/>; falha → <c>ProblemDetails</c> com o status do erro.</summary>
    protected IActionResult FromResult<T>(Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : ProblemFrom(result.Errors);

    protected IActionResult FromResult<T>(Result<T> result) => FromResult(result, value => Ok(value));

    protected IActionResult NoContentFrom<T>(Result<T> result) => FromResult(result, _ => NoContent());
}
