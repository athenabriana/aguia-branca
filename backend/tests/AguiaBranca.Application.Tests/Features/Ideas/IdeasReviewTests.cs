using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Ideas;
using AguiaBranca.Application.Features.Ideas.Approve;
using AguiaBranca.Application.Features.Ideas.Reject;
using AguiaBranca.Application.Features.Ideas.SaveIce;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Application.Tests.Features.Ideas;

public class SaveIceHandlerTests : IdeasTestBase
{
    private SaveIceHandler Handler() => new(TestServices.Validation(), Ideas, Responses(), As(Gestor), Uow, Clock);

    [Fact]
    public async Task FirstIce_MovesSubmittedToEmAnalise_WithTheScoreComputedByTheServer()
    {
        var idea = AddIdea(Operador);
        Clock.Advance(TimeSpan.FromHours(2));

        var result = await Handler().HandleAsync(new SaveIceCommand(idea.Id, 8, 7, 6), default);

        var body = result.Value;
        body.Status.Should().Be(IdeaStatus.EM_ANALISE);
        body.Ice.Should().Be(new IceResponse(8, 7, 6, 336));
        body.ReviewerId.Should().Be(Gestor.Id);
        body.UpdatedAt.Should().Be(Clock.UtcNow);
    }

    [Fact]
    public async Task SavingAgain_ReplacesTheIce_KeepsEmAnalise()
    {
        var idea = AddIdea(Operador, IdeaStatus.EM_ANALISE);

        var result = await Handler().HandleAsync(new SaveIceCommand(idea.Id, 10, 10, 10), default);

        (result.Value.Status, result.Value.Ice!.Score).Should().Be((IdeaStatus.EM_ANALISE, 1000));
    }

    [Theory]
    [InlineData(IdeaStatus.APROVADA)]
    [InlineData(IdeaStatus.REJEITADA)]
    [InlineData(IdeaStatus.IMPLEMENTADA)]
    public async Task OnClosedIdeas_Returns409_AndKeepsTheStatus(IdeaStatus status)
    {
        var idea = AddIdea(Operador, status);

        var result = await Handler().HandleAsync(new SaveIceCommand(idea.Id, 5, 5, 5), default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "IDEA_INVALID_STATE", Type = ErrorType.Conflict });
        idea.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(0, 5, 5, "impact")]
    [InlineData(5, 11, 5, "confidence")]
    [InlineData(5, 5, -1, "ease")]
    [InlineData(null, 5, 5, "impact")]
    [InlineData(5, null, 5, "confidence")]
    [InlineData(5, 5, null, "ease")]
    public async Task OutOfRangeOrMissing_Returns400_WithTheField(int? impact, int? confidence, int? ease, string field)
    {
        var idea = AddIdea(Operador);

        var result = await Handler().HandleAsync(new SaveIceCommand(idea.Id, impact, confidence, ease), default);

        result.Errors.Should().Contain(e => e.Field == field && e.Type == ErrorType.Validation);
        idea.Status.Should().Be(IdeaStatus.SUBMETIDA);
        idea.Ice.Should().BeNull();
    }

    [Fact]
    public async Task BoundaryValues_1And10_AreAccepted()
    {
        var idea = AddIdea(Operador);
        (await Handler().HandleAsync(new SaveIceCommand(idea.Id, 1, 1, 1), default)).Value.Ice!.Score.Should().Be(1);
        (await Handler().HandleAsync(new SaveIceCommand(idea.Id, 10, 10, 10), default)).Value.Ice!.Score.Should().Be(1000);
    }

    [Fact]
    public async Task UnknownOrMalformedId_Returns404()
    {
        (await Handler().HandleAsync(new SaveIceCommand(EntityId.New(), 5, 5, 5), default)).FirstError.ToStatusCode().Should().Be(404);
        (await Handler().HandleAsync(new SaveIceCommand("lixo", 5, 5, 5), default)).FirstError.ToStatusCode().Should().Be(404);
    }
}

public class RejectIdeaHandlerTests : IdeasTestBase
{
    private RejectIdeaHandler Handler() => new(TestServices.Validation(), Ideas, Responses(), As(Gestor), Uow, Clock);

    [Theory]
    [InlineData(IdeaStatus.SUBMETIDA)]
    [InlineData(IdeaStatus.EM_ANALISE)]
    public async Task WithComment_Rejects_SetsReviewer_AndGivesNoPoints(IdeaStatus from)
    {
        var idea = AddIdea(Operador, from);

        var result = await Handler().HandleAsync(new RejectIdeaCommand(idea.Id, "  Fora do escopo atual  "), default);

        var body = result.Value;
        (body.Status, body.ReviewComment, body.ReviewerId).Should().Be((IdeaStatus.REJEITADA, "Fora do escopo atual", Gestor.Id));
        body.ReviewedAt.Should().Be(Clock.UtcNow);
        Operador.Points.Should().Be(0);
        Events.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithoutComment_Returns400_AndTheIdeaIsUntouched(string? comment)
    {
        var idea = AddIdea(Operador);

        var result = await Handler().HandleAsync(new RejectIdeaCommand(idea.Id, comment), default);

        result.Errors.Should().Contain(e => e.Field == "comment" && e.Type == ErrorType.Validation);
        idea.Status.Should().Be(IdeaStatus.SUBMETIDA);
    }

    [Fact]
    public async Task CommentTooLong_Returns400()
    {
        var idea = AddIdea(Operador);
        (await Handler().HandleAsync(new RejectIdeaCommand(idea.Id, new string('x', 1001)), default)).Errors
            .Should().Contain(e => e.Field == "comment");
    }

    [Theory]
    [InlineData(IdeaStatus.APROVADA)]
    [InlineData(IdeaStatus.REJEITADA)]
    [InlineData(IdeaStatus.IMPLEMENTADA)]
    public async Task OnClosedIdeas_Returns409(IdeaStatus status)
    {
        var idea = AddIdea(Operador, status);
        (await Handler().HandleAsync(new RejectIdeaCommand(idea.Id, "tarde"), default)).FirstError.Code.Should().Be("IDEA_INVALID_STATE");
    }

    [Fact]
    public async Task UnknownId_Returns404() =>
        (await Handler().HandleAsync(new RejectIdeaCommand(EntityId.New(), "x"), default)).FirstError.ToStatusCode().Should().Be(404);
}

public class ApproveIdeaHandlerTests : IdeasTestBase
{
    private ApproveIdeaHandler Handler(IUnitOfWork? uow = null, AppUser? actor = null) => new(
        Ideas, Projects, ProjectUpdates, Users, Gamification(), As(actor ?? Gestor), uow ?? Uow, Clock);

    [Fact]
    public async Task Approve_CreatesTheDraftProject_InheritingFromTheIdea()
    {
        var idea = AddIdea(Operador, guidelineId: Guideline.Id, ice: new Ice(8, 7, 6), title: "Roteirização", division: Division.COMERCIO);

        var result = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AlreadyApproved.Should().BeFalse();
        idea.Status.Should().Be(IdeaStatus.APROVADA);
        idea.ReviewerId.Should().Be(Gestor.Id);

        var project = Projects.Items.Should().ContainSingle().Subject;
        result.Value.ProjectId.Should().Be(project.Id);
        project.Title.Should().Be("PROJ: Roteirização");
        project.Stage.Should().Be(ProjectStage.PLANEJAMENTO);
        project.OriginatingIdeaId.Should().Be(idea.Id);
        (project.GuidelineId, project.Division).Should().Be((Guideline.Id, Division.COMERCIO));
        (project.CreatorManagerId, project.ResponsibleId, project.ResponsibleName).Should().Be((Gestor.Id, Gestor.Id, "Gestor"));
        (project.ReporterId, project.ReporterName).Should().Be((Operador.Id, "Operador"));
        project.PriorityScore.Should().Be(336);
        (project.Investment, project.FinancialReturn).Should().Be((0m, 0m));
    }

    [Fact]
    public async Task Approve_RecordsTheFirstHistoryEntry_OfTheProject()
    {
        var idea = AddIdea(Operador, title: "Coleta seletiva");

        var result = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        var update = ProjectUpdates.Items.Should().ContainSingle().Subject;
        update.ProjectId.Should().Be(result.Value.ProjectId);
        update.Note.Should().Be("Criado automaticamente a partir da ideia: Coleta seletiva");
        (update.AuthorId, update.AuthorName).Should().Be((Gestor.Id, "Gestor"));
        update.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task Approve_Awards50ToTheAuthor_AndRecordsTheEvent()
    {
        var idea = AddIdea(Operador);

        await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        Operador.Points.Should().Be(50);
        var evt = Events.Items.Should().ContainSingle().Subject;
        (evt.UserId, evt.Delta, evt.Reason, evt.RefId).Should().Be((Operador.Id, 50, PointReason.IDEA_APPROVED, idea.Id));
        Gestor.Points.Should().Be(0, "o revisor não pontua");
    }

    [Fact]
    public async Task Approve_GrantsEstrategista_WhenTheIdeaHadAGuideline()
    {
        var linked = AddIdea(Operador, guidelineId: Guideline.Id);
        await Handler().HandleAsync(new ApproveIdeaCommand(linked.Id), default);
        Operador.Badges.Should().Contain([Badges.PrimeiraIdeia, Badges.Estrategista]);
    }

    [Fact]
    public async Task Approve_WithoutGuideline_DoesNotGrantEstrategista()
    {
        var idea = AddIdea(Operador);
        await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);
        Operador.Badges.Should().NotContain(Badges.Estrategista);
    }

    [Fact]
    public async Task Approve_ThirdDistinctGuideline_GrantsVisionario()
    {
        var g2 = Domain.Entities.Guideline.Create("Segunda", "d", Pillar.PROJETOS, null, Lider.Id, Lider.Name, Clock.UtcNow);
        var g3 = Domain.Entities.Guideline.Create("Terceira", "d", Pillar.MENSURACAO, null, Lider.Id, Lider.Name, Clock.UtcNow);
        Guidelines.Items.AddRange([g2, g3]);
        var ideas = new[] { AddIdea(Operador, guidelineId: Guideline.Id), AddIdea(Operador, guidelineId: g2.Id), AddIdea(Operador, guidelineId: g3.Id) };

        foreach (var idea in ideas.Take(2)) await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);
        Operador.Badges.Should().NotContain(Badges.Visionario);

        await Handler().HandleAsync(new ApproveIdeaCommand(ideas[2].Id), default);
        Operador.Badges.Should().Contain(Badges.Visionario);
    }

    [Fact]
    public async Task ApprovingTwice_IsIdempotent_SameProject_NoExtraPointsOrProjects()
    {
        var idea = AddIdea(Operador, guidelineId: Guideline.Id);

        var first = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);
        var second = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        second.IsSuccess.Should().BeTrue();
        (second.Value.AlreadyApproved, second.Value.ProjectId).Should().Be((true, first.Value.ProjectId));
        Projects.Items.Should().ContainSingle();
        ProjectUpdates.Items.Should().ContainSingle();
        Operador.Points.Should().Be(50);
        Events.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ApprovingAnAlreadyImplementedIdea_IsAlsoAnIdempotentNoOp()
    {
        var idea = AddIdea(Operador, IdeaStatus.IMPLEMENTADA);
        var project = Project.CreateDraftFromIdea(idea, Gestor.Id, Gestor.Name, Clock.UtcNow);
        Projects.Items.Add(project);

        var result = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        (result.Value.AlreadyApproved, result.Value.ProjectId).Should().Be((true, project.Id));
        idea.Status.Should().Be(IdeaStatus.IMPLEMENTADA);
        Events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task TheAuthorCannotApproveTheirOwnIdea_403_AndNothingChanges()
    {
        var idea = AddIdea(Gestor, title: "Ideia do próprio gestor");

        var result = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "SELF_APPROVAL_FORBIDDEN", Type = ErrorType.Forbidden });
        idea.Status.Should().Be(IdeaStatus.SUBMETIDA);
        Projects.Items.Should().BeEmpty();
        Events.Items.Should().BeEmpty();
        Gestor.Points.Should().Be(0);
    }

    [Fact]
    public async Task ARejectedIdea_CannotBeApproved_409()
    {
        var idea = AddIdea(Operador, IdeaStatus.REJEITADA);

        var result = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        result.FirstError.Code.Should().Be("IDEA_INVALID_STATE");
        Projects.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownOrMalformedId_Returns404()
    {
        (await Handler().HandleAsync(new ApproveIdeaCommand(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
        (await Handler().HandleAsync(new ApproveIdeaCommand("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
    }

    [Fact]
    public async Task IfTheAuthorNoLongerExists_TheApprovalStillHappens_WithoutPoints()
    {
        var idea = AddIdea(Operador);
        Users.Items.Remove(Operador);

        var result = await Handler().HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        result.IsSuccess.Should().BeTrue();
        Projects.Items.Should().ContainSingle();
        Events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task LosingTheRace_ADuplicateProject_IsReportedAsAlreadyApproved_WithTheWinnersProject()
    {
        var idea = AddIdea(Operador);
        // Outra requisição já aprovou e criou o projeto; o nosso commit bate no índice único de originatingIdeaId.
        var winners = Project.CreateDraftFromIdea(idea, Gestor.Id, Gestor.Name, Clock.UtcNow);

        var result = await Handler(new RaceLosingUnitOfWork(() => { Projects.Items.Clear(); Projects.Items.Add(winners); })).HandleAsync(new ApproveIdeaCommand(idea.Id), default);

        result.IsSuccess.Should().BeTrue();
        (result.Value.AlreadyApproved, result.Value.ProjectId).Should().Be((true, winners.Id));
    }

    /// <summary>
    /// Simula o commit que falha por chave duplicada: a transação do vencedor confirmou (só o projeto dele existe no banco)
    /// e a nossa sofre rollback.
    /// </summary>
    private sealed class RaceLosingUnitOfWork(Action winnerCommits) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(0);

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        {
            await work(ct);
            winnerCommits();
            throw new DuplicateKeyException("ux_projects_originatingIdeaId");
        }
    }
}
