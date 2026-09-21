using AguiaBranca.Api.Authorization;
using AguiaBranca.Api.Contracts;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Features.Projects;
using AguiaBranca.Application.Features.Projects.Create;
using AguiaBranca.Application.Features.Projects.Delete;
using AguiaBranca.Application.Features.Projects.Get;
using AguiaBranca.Application.Features.Projects.List;
using AguiaBranca.Application.Features.Projects.Update;
using AguiaBranca.Application.Features.Projects.Updates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguiaBranca.Api.Controllers;

/// <summary>Projetos e iniciativas. Leitura: gestor e líder. Escrita: somente gestor. O operador não acessa.</summary>
public sealed class ProjectsController : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ProjectsRead)]
    [ProducesResponseType<PagedResult<ProjectResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] ProjectListRequest filter, [FromServices] ListProjectsHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new ListProjectsQuery(filter.Stage, filter.Division, filter.GuidelineId, filter.Page, filter.PageSize), ct));

    [HttpGet("{id:objectid}")]
    [Authorize(Policy = Policies.ProjectsRead)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string id, [FromServices] GetProjectHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new GetProjectQuery(id), ct));

    /// <summary>Histórico do projeto (timeline), mais recente primeiro, com o diff dos campos alterados.</summary>
    [HttpGet("{id:objectid}/updates")]
    [Authorize(Policy = Policies.ProjectsRead)]
    [ProducesResponseType<PagedResult<ProjectUpdateResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Updates(
        string id, [FromServices] ListProjectUpdatesHandler handler, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        FromResult(await handler.HandleAsync(new ListProjectUpdatesQuery(id, page, pageSize), ct));

    /// <summary>Cadastro direto de projeto (sem partir de uma ideia). Só gestor.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.GestorOnly)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ProjectRequest r, [FromServices] CreateProjectHandler handler, CancellationToken ct) =>
        FromResult(
            await handler.HandleAsync(new CreateProjectCommand(
                r.Title, r.Description, r.Stage, r.StatusText, r.Investment, r.TargetDate, r.FinancialReturn,
                r.ProductivityGain, r.CostReduction, r.Division, r.GuidelineId, r.ResponsibleId), ct),
            created => CreatedAtAction(nameof(Get), new { id = created.Id }, created));

    /// <summary>
    /// Edita o projeto (substituição completa). Cada edição gera uma entrada de histórico com o diff. Ao mudar o estágio para
    /// <c>CONCLUIDO</c>, a ideia de origem vira <c>IMPLEMENTADA</c> e o autor recebe +200 pontos (uma única vez). Só gestor.
    /// </summary>
    [HttpPut("{id:objectid}")]
    [Authorize(Policy = Policies.GestorOnly)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        string id, [FromBody] ProjectRequest r, [FromServices] UpdateProjectHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new UpdateProjectCommand(
            id, r.Title, r.Description, r.Stage, r.StatusText, r.Investment, r.TargetDate, r.FinancialReturn,
            r.ProductivityGain, r.CostReduction, r.Division, r.GuidelineId, r.ResponsibleId, r.Note, r.Version), ct));

    /// <summary>Exclui o projeto e o seu histórico. A ideia de origem permanece como está. Só gestor.</summary>
    [HttpDelete("{id:objectid}")]
    [Authorize(Policy = Policies.GestorOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, [FromServices] DeleteProjectHandler handler, CancellationToken ct) =>
        NoContentFrom(await handler.HandleAsync(new DeleteProjectCommand(id), ct));
}
