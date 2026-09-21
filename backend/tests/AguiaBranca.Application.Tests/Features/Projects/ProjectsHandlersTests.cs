using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Ideas;
using AguiaBranca.Application.Features.Projects;
using AguiaBranca.Application.Features.Projects.Create;
using AguiaBranca.Application.Features.Projects.Delete;
using AguiaBranca.Application.Features.Projects.Get;
using AguiaBranca.Application.Features.Projects.List;
using AguiaBranca.Application.Features.Projects.Update;
using AguiaBranca.Application.Features.Projects.Updates;
using AguiaBranca.Application.Tests.Features.Ideas;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Application.Tests.Features.Projects;

public abstract class ProjectsTestBase : IdeasTestBase
{
    protected CreateProjectHandler CreateHandler(AppUser? actor = null) => new(
        TestServices.Validation(), Guidelines, Users, Projects, ProjectUpdates, new ProjectResponseFactory(Guidelines), As(actor ?? Gestor), Uow, Clock);

    protected UpdateProjectHandler UpdateHandler(AppUser? actor = null) => new(
        TestServices.Validation(), Guidelines, Users, Projects, ProjectUpdates,
        new ProjectCompletionAutomation(Ideas, Users, Gamification(), Clock), new ProjectResponseFactory(Guidelines), As(actor ?? Gestor), Uow, Clock);

    protected static CreateProjectCommand NewProject(
        string? title = "Projeto de teste", ProjectStage? stage = null, decimal? investment = null, string? guidelineId = null,
        string? responsibleId = null, DateTime? targetDate = null, Division? division = Division.LOGISTICA) =>
        new(title, "Descrição", stage, "Em andamento", investment, targetDate, null, null, null, division, guidelineId, responsibleId);

    protected static UpdateProjectCommand Edit(
        string id, string title = "Projeto de teste", ProjectStage stage = ProjectStage.PLANEJAMENTO, decimal investment = 0m,
        decimal financialReturn = 0m, decimal productivityGain = 0m, decimal costReduction = 0m, string? statusText = "Em andamento",
        string? guidelineId = null, string? responsibleId = null, DateTime? targetDate = null, string? note = null, int? version = null,
        Division division = Division.LOGISTICA, string? description = "Descrição") =>
        new(id, title, description, stage, statusText, investment, targetDate, financialReturn, productivityGain, costReduction,
            division, guidelineId, responsibleId, note, version);

    protected async Task<ProjectResponse> CreateAsync(CreateProjectCommand? command = null) =>
        (await CreateHandler().HandleAsync(command ?? NewProject(), default)).Value;
}

public class CreateProjectHandlerTests : ProjectsTestBase
{
    [Fact]
    public async Task Create_AppliesDefaults_AndRecordsTheFirstHistoryEntry()
    {
        var result = await CreateHandler().HandleAsync(NewProject(), default);

        var p = result.Value;
        (p.Stage, p.Investment, p.FinancialReturn, p.ProductivityGain, p.CostReduction).Should().Be((ProjectStage.PLANEJAMENTO, 0m, 0m, 0m, 0m));
        (p.CreatorManagerId, p.ResponsibleId, p.ResponsibleName).Should().Be((Gestor.Id, Gestor.Id, "Gestor"));
        (p.OriginatingIdeaId, p.PriorityScore, p.ReporterId).Should().Be((null, null, null));
        (p.Version, p.NetProfit, p.RoiPercent).Should().Be((1, 0m, null));

        var entry = ProjectUpdates.Items.Should().ContainSingle().Subject;
        (entry.ProjectId, entry.Note, entry.AuthorId, entry.AuthorName).Should().Be((p.Id, "Projeto criado", Gestor.Id, "Gestor"));
        entry.Changes.Should().BeEmpty();
        entry.CreatedAt.Should().Be(Clock.UtcNow);
    }

    [Fact]
    public async Task Create_WithAllFields_KeepsThem_AndComputesRoi()
    {
        var target = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var command = new CreateProjectCommand("Projeto completo", "d", ProjectStage.EM_EXECUCAO, "Piloto", 100_000m, target, 250_000m, 12.5m, 3_000m,
            Division.COMERCIO, Guideline.Id, Lider.Id);

        var p = (await CreateHandler().HandleAsync(command, default)).Value;

        (p.Stage, p.Investment, p.TargetDate, p.FinancialReturn, p.ProductivityGain, p.CostReduction, p.Division)
            .Should().Be((ProjectStage.EM_EXECUCAO, 100_000m, target, 250_000m, 12.5m, 3_000m, Division.COMERCIO));
        (p.GuidelineId, p.GuidelineTitle).Should().Be((Guideline.Id, "Eficiência operacional"));
        (p.ResponsibleId, p.ResponsibleName).Should().Be((Lider.Id, "Líder"));
        (p.NetProfit, p.RoiPercent).Should().Be((150_000m, 150m));
    }

    [Fact]
    public async Task Create_TargetDateWithoutTimeZone_IsStoredAsUtc()
    {
        var p = (await CreateHandler().HandleAsync(NewProject(targetDate: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified)), default)).Value;
        p.TargetDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Create_UnknownGuideline_Returns422()
    {
        var result = await CreateHandler().HandleAsync(NewProject(guidelineId: EntityId.New()), default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "GUIDELINE_NOT_FOUND", Type = ErrorType.Unprocessable, Field = "guidelineId" });
        Projects.Items.Should().BeEmpty();
        ProjectUpdates.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("operator")]
    public async Task Create_InvalidResponsible_Returns422(string kind)
    {
        var id = kind == "operator" ? Operador.Id : EntityId.New();

        var result = await CreateHandler().HandleAsync(NewProject(responsibleId: id), default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "RESPONSIBLE_NOT_FOUND", Field = "responsibleId" });
        Projects.Items.Should().BeEmpty();
    }

    public static IEnumerable<object?[]> Invalid()
    {
        yield return ["title", null, null, Division.LOGISTICA];
        yield return ["title", "   ", null, Division.LOGISTICA];
        yield return ["title", new string('x', 201), null, Division.LOGISTICA];
        yield return ["division", "Título", null, null];
        yield return ["investment", "Título", -1m, Division.LOGISTICA];
        yield return ["investment", "Título", 2_000_000_000_000m, Division.LOGISTICA];
    }

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task Create_InvalidInput_Returns400_AndPersistsNothing(string field, string? title, decimal? investment, Division? division)
    {
        var result = await CreateHandler().HandleAsync(NewProject(title, investment: investment, division: division), default);

        result.Errors.Should().Contain(e => e.Field == field && e.Type == ErrorType.Validation);
        Projects.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_NegativeOtherAmounts_Return400()
    {
        var command = NewProject() with { FinancialReturn = -1m, CostReduction = -2m, ProductivityGain = -3m };
        var result = await CreateHandler().HandleAsync(command, default);
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["financialReturn", "costReduction", "productivityGain"]);
    }

    [Fact]
    public async Task Create_ProductivityGainAbove100_IsAllowed() =>
        (await CreateHandler().HandleAsync(NewProject() with { ProductivityGain = 250m }, default)).IsSuccess.Should().BeTrue();
}

public class UpdateProjectHandlerTests : ProjectsTestBase
{
    [Fact]
    public async Task InvestmentChange_ProducesExactlyOneChange_TypedAsNumbers_AndAnEntryWithTheNote()
    {
        var created = await CreateAsync(NewProject(investment: 100m));
        Clock.Advance(TimeSpan.FromHours(1));

        var result = await UpdateHandler().HandleAsync(Edit(created.Id, investment: 120m, note: "Ajuste do orçamento"), default);

        result.Value.Investment.Should().Be(120m);
        result.Value.Version.Should().Be(2);
        result.Value.UpdatedAt.Should().Be(Clock.UtcNow);
        var entry = ProjectUpdates.Items.Single(u => u.Note == "Ajuste do orçamento");
        var change = entry.Changes.Should().ContainSingle().Subject;
        var typed = FieldChangeResponse.Of(change);
        (typed.Field, typed.From, typed.To).Should().Be(("investment", 100m, 120m));
        (entry.AuthorId, entry.CreatedAt).Should().Be((Gestor.Id, Clock.UtcNow));
    }

    [Fact]
    public async Task NoRealChange_StillRecordsTheNote_WithAnEmptyDiff()
    {
        var created = await CreateAsync();

        var result = await UpdateHandler().HandleAsync(Edit(created.Id, note: "Só um registro"), default);

        result.IsSuccess.Should().BeTrue();
        var entry = ProjectUpdates.Items.Single(u => u.Note == "Só um registro");
        entry.Changes.Should().BeEmpty();
        result.Value.Version.Should().Be(2);
    }

    [Fact]
    public async Task MultipleFields_ReportOnlyTheChangedOnes_WithTypedValues()
    {
        var created = await CreateAsync();
        var target = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        await UpdateHandler().HandleAsync(Edit(created.Id, title: "Novo título", stage: ProjectStage.EM_EXECUCAO, financialReturn: 5m, targetDate: target), default);

        var changes = ProjectUpdates.Items.OrderBy(u => u.CreatedAt).Last().Changes.Select(FieldChangeResponse.Of).ToDictionary(c => c.Field);
        changes.Keys.Should().BeEquivalentTo(["title", "stage", "financialReturn", "targetDate"]);
        (changes["title"].From, changes["title"].To).Should().Be(("Projeto de teste", "Novo título"));
        (changes["stage"].From, changes["stage"].To).Should().Be(("PLANEJAMENTO", "EM_EXECUCAO"));
        (changes["financialReturn"].From, changes["financialReturn"].To).Should().Be((0m, 5m));
        (changes["targetDate"].From, changes["targetDate"].To).Should().Be((null, "2026-12-01T00:00:00Z"));
    }

    [Fact]
    public async Task ClearingTheTargetDate_IsADiff_WithNullTo()
    {
        var created = await CreateAsync(NewProject(targetDate: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)));

        await UpdateHandler().HandleAsync(Edit(created.Id, targetDate: null), default);

        var change = ProjectUpdates.Items.OrderBy(u => u.CreatedAt).Last().Changes.Single(c => c.Field == "targetDate");
        (change.From, change.To).Should().Be(("2026-12-01T00:00:00Z", null));
    }

    [Fact]
    public async Task ChangingTheResponsible_IsRecordedByName_AndResolvedFromTheUsers()
    {
        var created = await CreateAsync();

        var result = await UpdateHandler().HandleAsync(Edit(created.Id, responsibleId: Lider.Id), default);

        (result.Value.ResponsibleId, result.Value.ResponsibleName).Should().Be((Lider.Id, "Líder"));
        var change = ProjectUpdates.Items.OrderBy(u => u.CreatedAt).Last().Changes.Should().ContainSingle().Subject;
        (change.Field, change.From, change.To).Should().Be(("responsável", "Gestor", "Líder"));
    }

    [Fact]
    public async Task OmittingTheResponsible_KeepsTheCurrentOne()
    {
        var created = await CreateAsync(NewProject(responsibleId: Lider.Id));
        var result = await UpdateHandler().HandleAsync(Edit(created.Id), default);
        result.Value.ResponsibleId.Should().Be(Lider.Id);
    }

    [Fact]
    public async Task AnotherGestor_CanEditAnyProject()
    {
        var created = await CreateAsync();
        var other = AppUser.Create("Outro Gestor", "og@x.com", Role.GESTOR, Division.COMERCIO, Clock.UtcNow); Users.Items.Add(other);

        var result = await UpdateHandler(other).HandleAsync(Edit(created.Id, investment: 9m), default);

        result.IsSuccess.Should().BeTrue();
        ProjectUpdates.Items.OrderBy(u => u.CreatedAt).Last().AuthorId.Should().Be(other.Id);
        result.Value.CreatorManagerId.Should().Be(Gestor.Id, "o criador não muda");
    }

    [Fact]
    public async Task StaleVersion_Returns409_AndAppliesNothing()
    {
        var created = await CreateAsync(NewProject(investment: 1m));
        await UpdateHandler().HandleAsync(Edit(created.Id, investment: 2m), default);   // versão 2

        var result = await UpdateHandler().HandleAsync(Edit(created.Id, investment: 3m, version: 1), default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "CONCURRENCY_CONFLICT", Type = ErrorType.Conflict });
        Projects.Items.Single().Investment.Should().Be(2m);
        ProjectUpdates.Items.Should().HaveCount(2, "criação + a edição bem-sucedida");
    }

    [Fact]
    public async Task MatchingVersion_Succeeds()
    {
        var created = await CreateAsync();
        (await UpdateHandler().HandleAsync(Edit(created.Id, investment: 5m, version: 1), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownOrMalformedId_Returns404()
    {
        (await UpdateHandler().HandleAsync(Edit(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
        (await UpdateHandler().HandleAsync(Edit("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
    }

    [Fact]
    public async Task UnknownGuidelineOrResponsible_Return422()
    {
        var created = await CreateAsync();
        (await UpdateHandler().HandleAsync(Edit(created.Id, guidelineId: EntityId.New()), default)).FirstError.Code.Should().Be("GUIDELINE_NOT_FOUND");
        (await UpdateHandler().HandleAsync(Edit(created.Id, responsibleId: Operador.Id), default)).FirstError.Code.Should().Be("RESPONSIBLE_NOT_FOUND");
    }

    [Fact]
    public async Task PutRequiresStageAndAmounts_ToAvoidZeroingByOmission()
    {
        var created = await CreateAsync(NewProject(investment: 500m));
        var partial = new UpdateProjectCommand(created.Id, "Título", "d", null, "s", null, null, null, null, null, Division.LOGISTICA, null, null, null, null);

        var result = await UpdateHandler().HandleAsync(partial, default);

        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["stage", "investment", "financialReturn", "costReduction", "productivityGain"]);
        Projects.Items.Single().Investment.Should().Be(500m, "nada foi zerado");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2_000_000_000_000)]
    public async Task InvalidAmount_Returns400(long amount)
    {
        var created = await CreateAsync();
        (await UpdateHandler().HandleAsync(Edit(created.Id, investment: amount), default)).Errors.Should().Contain(e => e.Field == "investment");
    }

    [Fact]
    public async Task NoteTooLong_AndBadVersion_Return400()
    {
        var created = await CreateAsync();
        (await UpdateHandler().HandleAsync(Edit(created.Id, note: new string('n', 501)), default)).Errors.Should().Contain(e => e.Field == "note");
        (await UpdateHandler().HandleAsync(Edit(created.Id, version: 0), default)).Errors.Should().Contain(e => e.Field == "version");
    }
}

/// <summary>Automação 2 (B16): concluir o projeto conclui a ideia de origem e premia o autor — uma única vez.</summary>
public class ProjectCompletionTests : ProjectsTestBase
{
    private async Task<(Idea Idea, Project Project)> ApprovedIdeaWithProjectAsync(string? guidelineId = null)
    {
        var idea = AddIdea(Operador, IdeaStatus.APROVADA, guidelineId);
        var project = Project.CreateDraftFromIdea(idea, Gestor.Id, Gestor.Name, Clock.UtcNow);
        Projects.Items.Add(project);
        await Task.CompletedTask;
        return (idea, project);
    }

    private Task<Result<ProjectResponse>> Complete(Project p, ProjectStage stage = ProjectStage.CONCLUIDO) =>
        UpdateHandler().HandleAsync(Edit(p.Id, title: p.Title, stage: stage, investment: 100m, financialReturn: 300m, description: p.Description, statusText: p.StatusText), default);

    [Fact]
    public async Task Completing_MarksTheIdeaImplemented_Awards200_AndGrantsImpactoReal()
    {
        var (idea, project) = await ApprovedIdeaWithProjectAsync(Guideline.Id);

        var result = await Complete(project);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stage.Should().Be(ProjectStage.CONCLUIDO);
        idea.Status.Should().Be(IdeaStatus.IMPLEMENTADA);
        Operador.Points.Should().Be(200);
        var evt = Events.Items.Should().ContainSingle().Subject;
        (evt.UserId, evt.Delta, evt.Reason, evt.RefId).Should().Be((Operador.Id, 200, PointReason.IDEA_IMPLEMENTED, idea.Id));
        Operador.Badges.Should().Contain([Badges.ImpactoReal, Badges.PrimeiraIdeia, Badges.Estrategista]);
    }

    [Fact]
    public async Task RepeatingTheCompletion_DoesNotCreditAgain()
    {
        var (idea, project) = await ApprovedIdeaWithProjectAsync();
        await Complete(project);

        var again = await Complete(project);

        again.IsSuccess.Should().BeTrue();
        Operador.Points.Should().Be(200);
        Events.Items.Should().ContainSingle();
        idea.Status.Should().Be(IdeaStatus.IMPLEMENTADA);
    }

    [Fact]
    public async Task ReopeningAndCompletingAgain_DoesNotCreditAgain()
    {
        var (_, project) = await ApprovedIdeaWithProjectAsync();
        await Complete(project);
        await Complete(project, ProjectStage.EM_EXECUCAO);

        await Complete(project);

        Operador.Points.Should().Be(200, "a ideia já é IMPLEMENTADA: sem novo crédito");
        Events.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task OtherStageChanges_DoNotTouchTheIdea()
    {
        var (idea, project) = await ApprovedIdeaWithProjectAsync();

        await Complete(project, ProjectStage.EM_EXECUCAO);
        await Complete(project, ProjectStage.CANCELADO);

        idea.Status.Should().Be(IdeaStatus.APROVADA);
        Events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ProjectWithoutAnOriginatingIdea_CompletesWithoutSideEffects()
    {
        var created = await CreateAsync();

        var result = await UpdateHandler().HandleAsync(Edit(created.Id, stage: ProjectStage.CONCLUIDO), default);

        result.IsSuccess.Should().BeTrue();
        Events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task IfTheOriginatingIdeaIsMissing_TheEditStillSucceeds()
    {
        var (idea, project) = await ApprovedIdeaWithProjectAsync();
        Ideas.Items.Remove(idea);

        (await Complete(project)).IsSuccess.Should().BeTrue();
        Events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task IfTheAuthorIsMissing_TheIdeaIsStillImplemented_WithoutPoints()
    {
        var (idea, project) = await ApprovedIdeaWithProjectAsync();
        Users.Items.Remove(Operador);

        (await Complete(project)).IsSuccess.Should().BeTrue();
        idea.Status.Should().Be(IdeaStatus.IMPLEMENTADA);
        Events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task TheCompletionEntry_RecordsTheStageDiff()
    {
        var (_, project) = await ApprovedIdeaWithProjectAsync();
        await Complete(project);

        var changes = ProjectUpdates.Items.Single().Changes;
        changes.Select(c => c.Field).Should().Contain(["stage", "investment", "financialReturn"]);
    }
}

public class ProjectReadAndDeleteTests : ProjectsTestBase
{
    [Fact]
    public async Task Get_ReturnsTheProject_With404ForUnknownOrMalformed()
    {
        var created = await CreateAsync(NewProject(guidelineId: Guideline.Id));
        var handler = new GetProjectHandler(Projects, new ProjectResponseFactory(Guidelines));

        (await handler.HandleAsync(new GetProjectQuery(created.Id), default)).Value.GuidelineTitle.Should().Be("Eficiência operacional");
        (await handler.HandleAsync(new GetProjectQuery(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
        (await handler.HandleAsync(new GetProjectQuery("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
    }

    [Fact]
    public async Task Get_OfAnOrphanGuideline_HasNullTitle()
    {
        var created = await CreateAsync(NewProject(guidelineId: Guideline.Id));
        Guidelines.Items.Remove(Guideline);

        var result = await new GetProjectHandler(Projects, new ProjectResponseFactory(Guidelines)).HandleAsync(new GetProjectQuery(created.Id), default);

        (result.Value.GuidelineId, result.Value.GuidelineTitle).Should().Be((Guideline.Id, null));
    }

    [Fact]
    public async Task List_IsNewestFirst_AndPaged()
    {
        for (var i = 0; i < 5; i++) { Clock.Advance(TimeSpan.FromMinutes(1)); await CreateAsync(NewProject($"Projeto {i}")); }
        var handler = new ListProjectsHandler(TestServices.Validation(), Projects, new ProjectResponseFactory(Guidelines));

        var page = (await handler.HandleAsync(new ListProjectsQuery(Page: 2, PageSize: 2), default)).Value;

        page.Items.Select(p => p.Title).Should().Equal("Projeto 2", "Projeto 1");
        (page.TotalItems, page.TotalPages).Should().Be((5, 3));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 201)]
    public async Task List_InvalidPaging_Returns400(int page, int size) =>
        (await new ListProjectsHandler(TestServices.Validation(), Projects, new ProjectResponseFactory(Guidelines))
            .HandleAsync(new ListProjectsQuery(Page: page, PageSize: size), default)).FirstError.Type.Should().Be(ErrorType.Validation);

    [Fact]
    public async Task Updates_AreNewestFirst_AndPaged_ForAnExistingProject()
    {
        var created = await CreateAsync();
        for (var i = 1; i <= 3; i++)
        {
            Clock.Advance(TimeSpan.FromMinutes(1));
            await UpdateHandler().HandleAsync(Edit(created.Id, investment: i * 10m, note: $"Edição {i}"), default);
        }
        var handler = new ListProjectUpdatesHandler(TestServices.Validation(), Projects, ProjectUpdates);

        var page = (await handler.HandleAsync(new ListProjectUpdatesQuery(created.Id, 1, 3), default)).Value;

        page.Items.Select(u => u.Note).Should().Equal("Edição 3", "Edição 2", "Edição 1");
        page.TotalItems.Should().Be(4);
        page.Items[0].Changes.Should().ContainSingle(c => c.Field == "investment" && (decimal)c.To! == 30m && (decimal)c.From! == 20m);
    }

    [Fact]
    public async Task Updates_UnknownProject_Returns404()
    {
        var handler = new ListProjectUpdatesHandler(TestServices.Validation(), Projects, ProjectUpdates);
        (await handler.HandleAsync(new ListProjectUpdatesQuery(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
        (await handler.HandleAsync(new ListProjectUpdatesQuery("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
    }

    [Fact]
    public async Task Delete_RemovesTheProjectAndItsHistory_ButNotTheIdea()
    {
        var idea = AddIdea(Operador, IdeaStatus.APROVADA);
        var project = Project.CreateDraftFromIdea(idea, Gestor.Id, Gestor.Name, Clock.UtcNow);
        Projects.Items.Add(project);
        ProjectUpdates.Items.Add(ProjectUpdate.Create(project.Id, Gestor.Id, "Gestor", "criado", [], Clock.UtcNow));
        ProjectUpdates.Items.Add(ProjectUpdate.Create(EntityId.New(), Gestor.Id, "Gestor", "de outro projeto", [], Clock.UtcNow));

        var result = await new DeleteProjectHandler(Projects, ProjectUpdates, Uow).HandleAsync(new DeleteProjectCommand(project.Id), default);

        result.IsSuccess.Should().BeTrue();
        Projects.Items.Should().BeEmpty();
        ProjectUpdates.Items.Should().ContainSingle().Which.Note.Should().Be("de outro projeto");
        idea.Status.Should().Be(IdeaStatus.APROVADA);
        Ideas.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_UnknownOrMalformed_Returns404()
    {
        var handler = new DeleteProjectHandler(Projects, ProjectUpdates, Uow);
        (await handler.HandleAsync(new DeleteProjectCommand(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
        (await handler.HandleAsync(new DeleteProjectCommand("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
    }
}
