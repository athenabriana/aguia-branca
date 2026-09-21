using AguiaBranca.Api.Authorization;
using AguiaBranca.Api.Contracts;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Features.Guidelines;
using AguiaBranca.Application.Features.Guidelines.Create;
using AguiaBranca.Application.Features.Guidelines.Delete;
using AguiaBranca.Application.Features.Guidelines.Get;
using AguiaBranca.Application.Features.Guidelines.History;
using AguiaBranca.Application.Features.Guidelines.List;
using AguiaBranca.Application.Features.Guidelines.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguiaBranca.Api.Controllers;

/// <summary>Orientações estratégicas: leitura para todos os perfis; escrita somente do líder.</summary>
public sealed class GuidelinesController : ApiControllerBase
{
    /// <summary>Lista as orientações, da mais recentemente alterada para a mais antiga.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<GuidelineResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] ListGuidelinesHandler handler, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        FromResult(await handler.HandleAsync(new ListGuidelinesQuery(page, pageSize), ct));

    /// <summary>Registro histórico das estratégias (id, data, categoria, campanha). Sobrevive à exclusão da orientação.</summary>
    [HttpGet("history")]
    [ProducesResponseType<PagedResult<GuidelineHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(
        [FromQuery] GuidelineHistoryRequest filter, [FromServices] GuidelineHistoryHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(
            new GuidelineHistoryQuery(filter.GuidelineId, filter.Category, filter.Campaign, filter.From, filter.To, filter.Page, filter.PageSize), ct));

    [HttpGet("{id:objectid}")]
    [ProducesResponseType<GuidelineResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string id, [FromServices] GetGuidelineHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new GetGuidelineQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = Policies.LiderOnly)]
    [ProducesResponseType<GuidelineResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] GuidelineRequest request, [FromServices] CreateGuidelineHandler handler, CancellationToken ct) =>
        FromResult(
            await handler.HandleAsync(new CreateGuidelineCommand(request.Title, request.Description, request.Pillar, request.Campaign), ct),
            created => CreatedAtAction(nameof(Get), new { id = created.Id }, created));

    [HttpPut("{id:objectid}")]
    [Authorize(Policy = Policies.LiderOnly)]
    [ProducesResponseType<GuidelineResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        string id, [FromBody] GuidelineRequest request, [FromServices] UpdateGuidelineHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(
            new UpdateGuidelineCommand(id, request.Title, request.Description, request.Pillar, request.Campaign), ct));

    [HttpDelete("{id:objectid}")]
    [Authorize(Policy = Policies.LiderOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, [FromServices] DeleteGuidelineHandler handler, CancellationToken ct) =>
        NoContentFrom(await handler.HandleAsync(new DeleteGuidelineCommand(id), ct));
}
