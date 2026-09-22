using AguiaBranca.Api.Authorization;
using AguiaBranca.Api.Contracts;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Features.Ideas;
using AguiaBranca.Application.Features.Ideas.Create;
using AguiaBranca.Application.Features.Ideas.Approve;
using AguiaBranca.Application.Features.Ideas.Delete;
using AguiaBranca.Application.Features.Ideas.Get;
using AguiaBranca.Application.Features.Ideas.List;
using AguiaBranca.Application.Features.Ideas.Reject;
using AguiaBranca.Application.Features.Ideas.SaveIce;
using AguiaBranca.Application.Features.Ideas.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguiaBranca.Api.Controllers;

/// <summary>Ideias de inovação. Operador só enxerga as próprias; gestor e líder enxergam todas.</summary>
public sealed class IdeasController : ApiControllerBase
{
    /// <summary>Lista ideias (<c>scope</c>: mine, curation, all).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<IdeaResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] IdeaListRequest filter, [FromServices] ListIdeasHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(
            new ListIdeasQuery(filter.Scope, filter.Status, filter.GuidelineId, filter.Division, filter.Page, filter.PageSize), ct));

    [HttpGet("{id:objectid}")]
    [ProducesResponseType<IdeaResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string id, [FromServices] GetIdeaHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new GetIdeaQuery(id), ct));

    /// <summary>Cadastra uma ideia (operador ou gestor). Credita +10 pontos (+5 se vinculada a uma orientação).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanCreateIdea)]
    [ProducesResponseType<IdeaResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] IdeaRequest request, [FromServices] CreateIdeaHandler handler, CancellationToken ct) =>
        FromResult(
            await handler.HandleAsync(new CreateIdeaCommand(request.Title, request.Description, request.Category, request.Division, request.GuidelineId), ct),
            created => CreatedAtAction(nameof(Get), new { id = created.Id }, created));

    /// <summary>Edita a própria ideia enquanto SUBMETIDA.</summary>
    [HttpPut("{id:objectid}")]
    [ProducesResponseType<IdeaResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        string id, [FromBody] IdeaRequest request, [FromServices] UpdateIdeaHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(
            new UpdateIdeaCommand(id, request.Title, request.Description, request.Category, request.Division, request.GuidelineId), ct));

    /// <summary>Exclui a própria ideia enquanto SUBMETIDA; estorna os pontos.</summary>
    [HttpDelete("{id:objectid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, [FromServices] DeleteIdeaHandler handler, CancellationToken ct) =>
        NoContentFrom(await handler.HandleAsync(new DeleteIdeaCommand(id), ct));

    /// <summary>Salva a matriz ICE (1–10 cada; score calculado no servidor). SUBMETIDA passa a EM_ANALISE. Só gestor.</summary>
    [HttpPut("{id:objectid}/ice")]
    [Authorize(Policy = Policies.GestorOnly)]
    [ProducesResponseType<IdeaResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveIce(
        string id, [FromBody] IceRequest request, [FromServices] SaveIceHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new SaveIceCommand(id, request.Impact, request.Confidence, request.Ease), ct));

    /// <summary>Rejeita a ideia (comentário obrigatório). Só gestor.</summary>
    [HttpPost("{id:objectid}/reject")]
    [Authorize(Policy = Policies.GestorOnly)]
    [ProducesResponseType<IdeaResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(
        string id, [FromBody] RejectRequest request, [FromServices] RejectIdeaHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new RejectIdeaCommand(id, request.Comment), ct));

    /// <summary>
    /// Aprova a ideia: cria o projeto rascunho, credita +50 ao autor e avalia badges, numa transação. Idempotente.
    /// O autor não pode aprovar a própria ideia. Só gestor.
    /// </summary>
    [HttpPost("{id:objectid}/approve")]
    [Authorize(Policy = Policies.GestorOnly)]
    [ProducesResponseType<ApproveIdeaResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(string id, [FromServices] ApproveIdeaHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new ApproveIdeaCommand(id), ct));
}
