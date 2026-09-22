using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Ideas;
using AguiaBranca.Application.Features.Ideas.Create;
using AguiaBranca.Application.Features.Ideas.Delete;
using AguiaBranca.Application.Features.Ideas.Get;
using AguiaBranca.Application.Features.Ideas.List;
using AguiaBranca.Application.Features.Ideas.Update;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Application.Tests.Features.Ideas;

public class CreateIdeaHandlerTests : IdeasTestBase
{
    private CreateIdeaHandler Handler(AppUser? as_ = null) => new(
        TestServices.Validation(), Ideas, Guidelines, Users, Gamification(), Responses(), As(as_ ?? Operador), Uow, Clock);

    private static CreateIdeaCommand Cmd(string? guidelineId = null, Division? division = null, string title = "Roteirização com IA", string? category = "tecnologia") =>
        new(title, "Descrição da ideia", category, division, guidelineId);

    [Fact]
    public async Task WithGuideline_Awards15_RecordsTheEvent_AndReturnsIt()
    {
        var result = await Handler().HandleAsync(Cmd(Guideline.Id), default);

        result.IsSuccess.Should().BeTrue();
        var body = result.Value;
        (body.Status, body.PointsAwarded, body.GuidelineTitle).Should().Be((IdeaStatus.SUBMETIDA, 15, "Eficiência operacional"));
        (body.AuthorId, body.AuthorName).Should().Be((Operador.Id, "Operador"));
        body.Category.Should().Be("Tecnologia", "normalizada: primeira letra maiúscula");
        body.Ice.Should().BeNull();
        body.LinkedProject.Should().BeNull();

        Operador.Points.Should().Be(15);
        var evt = Events.Items.Should().ContainSingle().Subject;
        (evt.UserId, evt.Delta, evt.Reason, evt.RefId).Should().Be((Operador.Id, 15, PointReason.IDEA_CREATED, body.Id));
        Ideas.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task WithoutGuideline_Awards10()
    {
        var result = await Handler().HandleAsync(Cmd(), default);

        result.Value.PointsAwarded.Should().Be(10);
        result.Value.GuidelineId.Should().BeNull();
        Operador.Points.Should().Be(10);
    }

    [Fact]
    public async Task BlankGuidelineId_IsTreatedAsNoGuideline()
    {
        var result = await Handler().HandleAsync(Cmd("   "), default);
        (result.Value.GuidelineId, result.Value.PointsAwarded).Should().Be((null, 10));
    }

    [Fact]
    public async Task DivisionDefaultsToTheAuthorsDivision_WhenOmitted()
    {
        (await Handler().HandleAsync(Cmd(), default)).Value.Division.Should().Be(Division.LOGISTICA);
        (await Handler().HandleAsync(Cmd(division: Division.COMERCIO), default)).Value.Division.Should().Be(Division.COMERCIO);
    }

    [Theory]
    [InlineData("665f00000000000000000abc")]
    [InlineData("nao-e-objectid")]
    public async Task UnknownGuideline_Returns422_AndPersistsNothing(string guidelineId)
    {
        var result = await Handler().HandleAsync(Cmd(guidelineId), default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "GUIDELINE_NOT_FOUND", Type = ErrorType.Unprocessable, Field = "guidelineId" });
        Ideas.Items.Should().BeEmpty();
        Events.Items.Should().BeEmpty();
        Operador.Points.Should().Be(0);
    }

    [Theory]
    [InlineData("", "tecnologia", "title")]
    [InlineData("ab", "tecnologia", "title")]
    [InlineData("Título válido", null, "category")]
    [InlineData("Título válido", "x", "category")]
    public async Task InvalidInput_Returns400(string title, string? category, string field)
    {
        var result = await Handler().HandleAsync(Cmd(title: title, category: category), default);

        result.Errors.Should().Contain(e => e.Field == field && e.Type == ErrorType.Validation);
        Ideas.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidDivision_Returns400()
    {
        var result = await Handler().HandleAsync(Cmd(division: (Division)99), default);
        result.Errors.Should().Contain(e => e.Field == "division");
    }

    [Fact]
    public async Task FirstIdea_GrantsPrimeiraIdeia_AndPersistsItOnTheUser()
    {
        await Handler().HandleAsync(Cmd(), default);

        Operador.Badges.Should().Equal(Badges.PrimeiraIdeia);
    }

    [Fact]
    public async Task FifthIdeaInTheSameMonth_GrantsInovadorDoMes_OnlyOnce()
    {
        for (var i = 0; i < 4; i++) await Handler().HandleAsync(Cmd(title: $"Ideia número {i}"), default);
        Operador.Badges.Should().NotContain(Badges.InovadorDoMes);

        await Handler().HandleAsync(Cmd(title: "Ideia número 5"), default);
        await Handler().HandleAsync(Cmd(title: "Ideia número 6"), default);

        Operador.Badges.Where(b => b == Badges.InovadorDoMes).Should().ContainSingle();
    }

    [Fact]
    public async Task GestorCanCreate_TooAndEarnsPointsFromTheirOwnIdea()
    {
        var result = await Handler(Gestor).HandleAsync(Cmd(Guideline.Id), default);

        result.Value.AuthorId.Should().Be(Gestor.Id);
        Gestor.Points.Should().Be(15);
    }

    [Fact]
    public async Task UnknownAuthor_Returns401()
    {
        var ghost = new FakeCurrentUser { Id = EntityId.New(), Role = Role.OPERADOR };
        var handler = new CreateIdeaHandler(TestServices.Validation(), Ideas, Guidelines, Users, Gamification(), Responses(), ghost, Uow, Clock);

        (await handler.HandleAsync(Cmd(), default)).FirstError.ToStatusCode().Should().Be(401);
    }
}

public class UpdateIdeaHandlerTests : IdeasTestBase
{
    private UpdateIdeaHandler Handler(AppUser as_) => new(TestServices.Validation(), Ideas, Guidelines, Responses(), As(as_), Uow, Clock);

    private static UpdateIdeaCommand Cmd(string id, string? guidelineId = null, string title = "Título revisado") =>
        new(id, title, "Nova descrição", "operações", Division.COMERCIO, guidelineId);

    [Fact]
    public async Task Author_EditsWhileSubmitted()
    {
        var idea = AddIdea(Operador);
        Clock.Advance(TimeSpan.FromHours(1));

        var result = await Handler(Operador).HandleAsync(Cmd(idea.Id, Guideline.Id), default);

        var body = result.Value;
        (body.Title, body.Category, body.Division, body.GuidelineTitle).Should().Be(("Título revisado", "Operações", Division.COMERCIO, "Eficiência operacional"));
        body.UpdatedAt.Should().Be(Clock.UtcNow);
        body.Status.Should().Be(IdeaStatus.SUBMETIDA);
    }

    [Fact]
    public async Task Editing_DoesNotChangePoints()
    {
        var idea = AddIdea(Operador);
        await Handler(Operador).HandleAsync(Cmd(idea.Id, Guideline.Id), default);

        Operador.Points.Should().Be(0);
        Events.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(IdeaStatus.EM_ANALISE)]
    [InlineData(IdeaStatus.APROVADA)]
    [InlineData(IdeaStatus.REJEITADA)]
    [InlineData(IdeaStatus.IMPLEMENTADA)]
    public async Task AfterReviewStarts_ReturnsIdeaNotEditable409(IdeaStatus status)
    {
        var idea = AddIdea(Operador, status);

        var result = await Handler(Operador).HandleAsync(Cmd(idea.Id), default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "IDEA_NOT_EDITABLE", Type = ErrorType.Conflict });
        idea.Title.Should().Be("Ideia de teste");
    }

    [Fact]
    public async Task AnotherOperator_Gets404_NotRevealingTheIdea()
    {
        var idea = AddIdea(Operador);
        var other = UserFactory.Operator("Outro"); Users.Items.Add(other);

        (await Handler(other).HandleAsync(Cmd(idea.Id), default)).FirstError.ToStatusCode().Should().Be(404);
        idea.Title.Should().Be("Ideia de teste");
    }

    [Theory]
    [InlineData(Role.GESTOR)]
    [InlineData(Role.LIDER)]
    public async Task ManagersAndLeaders_CanSeeButNotEditSomeoneElsesIdea_403(Role role)
    {
        var idea = AddIdea(Operador);
        var actor = role == Role.GESTOR ? Gestor : Lider;

        (await Handler(actor).HandleAsync(Cmd(idea.Id), default)).FirstError.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task GestorCanEditTheirOwnIdea()
    {
        var idea = AddIdea(Gestor);
        (await Handler(Gestor).HandleAsync(Cmd(idea.Id), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownGuideline_Returns422() =>
        (await Handler(Operador).HandleAsync(Cmd(AddIdea(Operador).Id, EntityId.New()), default))
            .FirstError.Code.Should().Be("GUIDELINE_NOT_FOUND");

    [Fact]
    public async Task MalformedOrUnknownId_Returns404()
    {
        (await Handler(Operador).HandleAsync(Cmd("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
        (await Handler(Operador).HandleAsync(Cmd(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
    }

    [Fact]
    public async Task InvalidInput_Returns400() =>
        (await Handler(Operador).HandleAsync(Cmd(AddIdea(Operador).Id, title: "x"), default)).FirstError.Type.Should().Be(ErrorType.Validation);
}

public class DeleteIdeaHandlerTests : IdeasTestBase
{
    private DeleteIdeaHandler Handler(AppUser as_) => new(Ideas, Users, Gamification(), As(as_), Uow);

    [Theory]
    [InlineData(false, 15, -10, 5)]
    [InlineData(true, 30, -15, 15)]
    public async Task Author_Deletes_AndTheCreationPointsAreRefunded(bool withGuideline, int startPoints, int refund, int expectedPoints)
    {
        var idea = AddIdea(Operador, guidelineId: withGuideline ? Guideline.Id : null);
        Operador.ApplyPoints(startPoints);

        var result = await Handler(Operador).HandleAsync(new DeleteIdeaCommand(idea.Id), default);

        result.IsSuccess.Should().BeTrue();
        Ideas.Items.Should().BeEmpty();
        Operador.Points.Should().Be(expectedPoints);
        var evt = Events.Items.Should().ContainSingle().Subject;
        (evt.Delta, evt.Reason, evt.RefId).Should().Be((refund, PointReason.IDEA_DELETED, idea.Id));
    }

    [Fact]
    public async Task Refund_ClampsAtZero_AndTheEventKeepsTheEffectiveValue()
    {
        var idea = AddIdea(Operador, guidelineId: Guideline.Id);
        Operador.ApplyPoints(4);

        await Handler(Operador).HandleAsync(new DeleteIdeaCommand(idea.Id), default);

        Operador.Points.Should().Be(0);
        Events.Items.Should().ContainSingle().Which.Delta.Should().Be(-4);
    }

    [Fact]
    public async Task WhenThereIsNothingToRefund_NoEventIsRecorded()
    {
        var idea = AddIdea(Operador);
        await Handler(Operador).HandleAsync(new DeleteIdeaCommand(idea.Id), default);
        Events.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(IdeaStatus.EM_ANALISE)]
    [InlineData(IdeaStatus.APROVADA)]
    [InlineData(IdeaStatus.IMPLEMENTADA)]
    public async Task AfterReviewStarts_409_AndNothingChanges(IdeaStatus status)
    {
        var idea = AddIdea(Operador, status);
        Operador.ApplyPoints(100);

        var result = await Handler(Operador).HandleAsync(new DeleteIdeaCommand(idea.Id), default);

        result.FirstError.Code.Should().Be("IDEA_NOT_EDITABLE");
        Ideas.Items.Should().ContainSingle();
        Operador.Points.Should().Be(100);
        Events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyTheAuthor_CanDelete()
    {
        var idea = AddIdea(Operador);
        var other = UserFactory.Operator("Outro"); Users.Items.Add(other);

        (await Handler(other).HandleAsync(new DeleteIdeaCommand(idea.Id), default)).FirstError.ToStatusCode().Should().Be(404);
        (await Handler(Gestor).HandleAsync(new DeleteIdeaCommand(idea.Id), default)).FirstError.ToStatusCode().Should().Be(403);
        Ideas.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task UnknownOrMalformedId_Returns404()
    {
        (await Handler(Operador).HandleAsync(new DeleteIdeaCommand(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
        (await Handler(Operador).HandleAsync(new DeleteIdeaCommand("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
    }
}

public class GetIdeaHandlerTests : IdeasTestBase
{
    private GetIdeaHandler Handler(AppUser as_) => new(Ideas, Responses(), As(as_));

    [Fact]
    public async Task Operator_SeesOwn_ButNotOthers()
    {
        var mine = AddIdea(Operador);
        var theirs = AddIdea(Gestor);

        (await Handler(Operador).HandleAsync(new GetIdeaQuery(mine.Id), default)).IsSuccess.Should().BeTrue();
        (await Handler(Operador).HandleAsync(new GetIdeaQuery(theirs.Id), default)).FirstError.ToStatusCode().Should().Be(404);
    }

    [Theory]
    [InlineData(Role.GESTOR)]
    [InlineData(Role.LIDER)]
    public async Task ManagersAndLeaders_SeeEveryIdea(Role role)
    {
        var idea = AddIdea(Operador);
        var actor = role == Role.GESTOR ? Gestor : Lider;

        (await Handler(actor).HandleAsync(new GetIdeaQuery(idea.Id), default)).Value.Id.Should().Be(idea.Id);
    }

    [Fact]
    public async Task IncludesTheLinkedProject_ForTheJourney()
    {
        var idea = AddIdea(Operador, IdeaStatus.APROVADA, Guideline.Id);
        var project = Project.CreateDraftFromIdea(idea, Gestor.Id, Gestor.Name, Clock.UtcNow);
        Projects.Items.Add(project);

        var body = (await Handler(Operador).HandleAsync(new GetIdeaQuery(idea.Id), default)).Value;

        body.LinkedProject.Should().BeEquivalentTo(new { project.Id, Stage = ProjectStage.PLANEJAMENTO });
    }

    [Fact]
    public async Task OrphanGuideline_HasNullTitle_ButKeepsTheId()
    {
        var idea = AddIdea(Operador, guidelineId: Guideline.Id);
        Guidelines.Items.Remove(Guideline);

        var body = (await Handler(Operador).HandleAsync(new GetIdeaQuery(idea.Id), default)).Value;

        (body.GuidelineId, body.GuidelineTitle).Should().Be((Guideline.Id, null));
    }

    [Fact]
    public async Task ExposesIce_WhenScored()
    {
        var idea = AddIdea(Operador, ice: new Ice(8, 7, 6));
        var body = (await Handler(Gestor).HandleAsync(new GetIdeaQuery(idea.Id), default)).Value;
        body.Ice.Should().Be(new IceResponse(8, 7, 6, 336));
    }

    [Fact]
    public async Task MalformedOrUnknownId_Returns404()
    {
        (await Handler(Gestor).HandleAsync(new GetIdeaQuery("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
        (await Handler(Gestor).HandleAsync(new GetIdeaQuery(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
    }
}

public class ListIdeasHandlerTests : IdeasTestBase
{
    private ListIdeasHandler Handler(AppUser as_) => new(TestServices.Validation(), Ideas, Responses(), As(as_));

    [Fact]
    public async Task Operator_DefaultsToMine_AndEvenScopeAllReturnsOnlyOwn()
    {
        AddIdea(Operador, title: "Minha ideia"); AddIdea(Gestor, title: "Ideia do gestor");

        var byDefault = (await Handler(Operador).HandleAsync(new ListIdeasQuery(), default)).Value;
        var scopeAll = (await Handler(Operador).HandleAsync(new ListIdeasQuery(IdeaScope.ALL), default)).Value;

        byDefault.Items.Select(i => i.Title).Should().Equal("Minha ideia");
        scopeAll.Items.Select(i => i.Title).Should().Equal("Minha ideia");
        Ideas.LastQuery!.AuthorId.Should().Be(Operador.Id);
    }

    [Fact]
    public async Task Gestor_DefaultsToAll_AndMineRestrictsToOwn()
    {
        AddIdea(Operador, title: "Do operador"); AddIdea(Gestor, title: "Do gestor");

        (await Handler(Gestor).HandleAsync(new ListIdeasQuery(), default)).Value.TotalItems.Should().Be(2);
        (await Handler(Gestor).HandleAsync(new ListIdeasQuery(IdeaScope.MINE), default)).Value.Items.Single().Title.Should().Be("Do gestor");
    }

    [Fact]
    public async Task Curation_ShowsOpenIdeasByIceScoreDescending_UnscoredLast()
    {
        var t = Clock.UtcNow;
        AddIdea(Operador, title: "Sem ICE antiga", createdAt: t.AddDays(-3));
        AddIdea(Operador, title: "ICE baixo", ice: new Ice(2, 2, 2), createdAt: t.AddDays(-2));
        AddIdea(Operador, title: "ICE alto", ice: new Ice(9, 9, 9), createdAt: t.AddDays(-1));
        AddIdea(Operador, title: "Sem ICE recente", createdAt: t);
        AddIdea(Operador, IdeaStatus.APROVADA, title: "Já aprovada");
        AddIdea(Operador, IdeaStatus.REJEITADA, title: "Rejeitada");

        var page = (await Handler(Gestor).HandleAsync(new ListIdeasQuery(IdeaScope.CURATION), default)).Value;

        page.Items.Select(i => i.Title).Should().Equal("ICE alto", "ICE baixo", "Sem ICE recente", "Sem ICE antiga");
        Ideas.LastQuery!.Statuses.Should().BeEquivalentTo([IdeaStatus.SUBMETIDA, IdeaStatus.EM_ANALISE]);
    }

    [Fact]
    public async Task Curation_CombinedWithAStatus_NarrowsOrIsEmpty()
    {
        AddIdea(Operador, title: "Aguardando"); AddIdea(Operador, IdeaStatus.EM_ANALISE, title: "Em análise");

        var analise = (await Handler(Gestor).HandleAsync(new ListIdeasQuery(IdeaScope.CURATION, IdeaStatus.EM_ANALISE), default)).Value;
        var impossible = (await Handler(Gestor).HandleAsync(new ListIdeasQuery(IdeaScope.CURATION, IdeaStatus.APROVADA), default)).Value;

        analise.Items.Select(i => i.Title).Should().Equal("Em análise");
        impossible.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Filters_ByStatusGuidelineAndDivision_ArePassedThrough()
    {
        AddIdea(Operador, guidelineId: Guideline.Id, division: Division.COMERCIO, title: "Alvo");
        AddIdea(Operador, title: "Outra");

        var result = (await Handler(Gestor).HandleAsync(
            new ListIdeasQuery(Status: IdeaStatus.SUBMETIDA, GuidelineId: Guideline.Id, Division: Division.COMERCIO), default)).Value;

        result.Items.Select(i => i.Title).Should().Equal("Alvo");
        result.Items[0].GuidelineTitle.Should().Be("Eficiência operacional");
    }

    [Fact]
    public async Task DefaultOrder_IsNewestFirst_AndPaged()
    {
        for (var i = 0; i < 5; i++) AddIdea(Operador, title: $"Ideia {i}", createdAt: Clock.UtcNow.AddMinutes(i));

        var page = (await Handler(Gestor).HandleAsync(new ListIdeasQuery(Page: 2, PageSize: 2), default)).Value;

        page.Items.Select(i => i.Title).Should().Equal("Ideia 2", "Ideia 1");
        (page.TotalItems, page.TotalPages).Should().Be((5, 3));
    }

    [Fact]
    public async Task Response_CarriesLinkedProjectPerIdea()
    {
        var approved = AddIdea(Operador, IdeaStatus.APROVADA); AddIdea(Operador, title: "Sem projeto");
        Projects.Items.Add(Project.CreateDraftFromIdea(approved, Gestor.Id, Gestor.Name, Clock.UtcNow));

        var items = (await Handler(Gestor).HandleAsync(new ListIdeasQuery(), default)).Value.Items;

        items.Single(i => i.Id == approved.Id).LinkedProject.Should().NotBeNull();
        items.Single(i => i.Title == "Sem projeto").LinkedProject.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 201)]
    public async Task InvalidPaging_Returns400(int page, int size) =>
        (await Handler(Gestor).HandleAsync(new ListIdeasQuery(Page: page, PageSize: size), default)).FirstError.Type.Should().Be(ErrorType.Validation);

    [Fact]
    public async Task InvalidEnums_Return400()
    {
        (await Handler(Gestor).HandleAsync(new ListIdeasQuery(Scope: (IdeaScope)9), default)).FirstError.Type.Should().Be(ErrorType.Validation);
        (await Handler(Gestor).HandleAsync(new ListIdeasQuery(Status: (IdeaStatus)9), default)).FirstError.Type.Should().Be(ErrorType.Validation);
        (await Handler(Gestor).HandleAsync(new ListIdeasQuery(Division: (Division)9), default)).FirstError.Type.Should().Be(ErrorType.Validation);
    }
}
