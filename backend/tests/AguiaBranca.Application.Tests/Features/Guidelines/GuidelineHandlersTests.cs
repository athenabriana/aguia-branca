using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Guidelines;
using AguiaBranca.Application.Features.Guidelines.Create;
using AguiaBranca.Application.Features.Guidelines.Delete;
using AguiaBranca.Application.Features.Guidelines.Get;
using AguiaBranca.Application.Features.Guidelines.History;
using AguiaBranca.Application.Features.Guidelines.List;
using AguiaBranca.Application.Features.Guidelines.Update;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Tests.Features.Guidelines;

public abstract class GuidelineTestBase
{
    protected readonly InMemoryGuidelines Guidelines = new();
    protected readonly InMemoryGuidelineHistory History = new();
    protected readonly FakeUnitOfWork Uow = new();
    protected readonly FakeClock Clock = new();
    protected readonly FakeCurrentUser Lider = new() { Id = "665f000000000000000000a1", Name = "Líder INOVAGAB", Role = Role.LIDER };

    protected Guideline Seed(string title = "Eficiência operacional", string? campaign = "Campanha 2026", Pillar pillar = Pillar.IDEIAS)
    {
        var g = Guideline.Create(title, "Descrição", pillar, campaign, Lider.Id, Lider.Name, Clock.UtcNow);
        Guidelines.Items.Add(g);
        return g;
    }
}

public class CreateGuidelineHandlerTests : GuidelineTestBase
{
    private CreateGuidelineHandler Handler() => new(TestServices.Validation(), Guidelines, History, Lider, Uow, Clock);

    [Fact]
    public async Task Create_PersistsGuidelineAndHistory_InOneTransaction_WithAuthorFromTheToken()
    {
        var result = await Handler().HandleAsync(new CreateGuidelineCommand("  Nova orientação  ", " desc ", Pillar.PROJETOS, " Campanha X "), default);

        result.IsSuccess.Should().BeTrue();
        var body = result.Value;
        body.Title.Should().Be("Nova orientação");
        body.Campaign.Should().Be("Campanha X");
        body.AuthorId.Should().Be(Lider.Id);
        body.AuthorName.Should().Be("Líder INOVAGAB");
        (body.CreatedAt, body.UpdatedAt).Should().Be((Clock.UtcNow, Clock.UtcNow));
        Guidelines.Items.Should().ContainSingle();

        var entry = History.Items.Should().ContainSingle().Subject;
        entry.Action.Should().Be(GuidelineAction.CREATED);
        entry.GuidelineId.Should().Be(body.Id);
        entry.Category.Should().Be(Pillar.PROJETOS);
        entry.Campaign.Should().Be("Campanha X");
        entry.ChangedById.Should().Be(Lider.Id);
        entry.OccurredAt.Should().Be(Clock.UtcNow);
        Uow.Transactions.Should().Be(1);
    }

    [Fact]
    public async Task Create_WithoutCampaign_StoresNull()
    {
        var result = await Handler().HandleAsync(new CreateGuidelineCommand("Título válido", "d", Pillar.IDEIAS, "   "), default);
        result.Value.Campaign.Should().BeNull();
        History.Items.Single().Campaign.Should().BeNull();
    }

    public static IEnumerable<object?[]> InvalidInputs()
    {
        yield return ["title", null, "d", Pillar.IDEIAS, null];
        yield return ["title", "  ", "d", Pillar.IDEIAS, null];
        yield return ["title", "ab", "d", Pillar.IDEIAS, null];
        yield return ["title", new string('x', 121), "d", Pillar.IDEIAS, null];
        yield return ["description", "Título válido", new string('d', 2001), Pillar.IDEIAS, null];
        yield return ["pillar", "Título válido", "d", null, null];
        yield return ["pillar", "Título válido", "d", (Pillar)99, null];
        yield return ["campaign", "Título válido", "d", Pillar.IDEIAS, new string('c', 81)];
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Create_InvalidInput_Returns400_AndPersistsNothing(string field, string? title, string? description, Pillar? pillar, string? campaign)
    {
        var result = await Handler().HandleAsync(new CreateGuidelineCommand(title, description, pillar, campaign), default);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == field && e.Type == ErrorType.Validation);
        Guidelines.Items.Should().BeEmpty();
        History.Items.Should().BeEmpty();
        Uow.Transactions.Should().Be(0);
    }
}

public class UpdateGuidelineHandlerTests : GuidelineTestBase
{
    private UpdateGuidelineHandler Handler() => new(TestServices.Validation(), Guidelines, History, Lider, Uow, Clock);

    [Fact]
    public async Task Update_ChangesFields_KeepsAuthorAndCreatedAt_AndRecordsHistoryWithTheNewSnapshot()
    {
        var original = Seed();
        var createdAt = original.CreatedAt;
        Clock.Advance(TimeSpan.FromHours(3));

        var result = await Handler().HandleAsync(
            new UpdateGuidelineCommand(original.Id, "Título editado", "Nova descrição", Pillar.MENSURACAO, "Campanha Y"), default);

        result.IsSuccess.Should().BeTrue();
        var body = result.Value;
        (body.Title, body.Pillar, body.Campaign).Should().Be(("Título editado", Pillar.MENSURACAO, "Campanha Y"));
        body.CreatedAt.Should().Be(createdAt);
        body.UpdatedAt.Should().Be(Clock.UtcNow);
        body.AuthorId.Should().Be(Lider.Id);

        var entry = History.Items.Should().ContainSingle().Subject;
        entry.Action.Should().Be(GuidelineAction.UPDATED);
        entry.Category.Should().Be(Pillar.MENSURACAO);
        entry.Campaign.Should().Be("Campanha Y");
        entry.Snapshot.Title.Should().Be("Título editado");
        entry.OccurredAt.Should().Be(Clock.UtcNow);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404_WithoutHistory()
    {
        var result = await Handler().HandleAsync(new UpdateGuidelineCommand(EntityId.New(), "Título válido", "d", Pillar.IDEIAS, null), default);
        result.FirstError.Code.Should().Be("RESOURCE_NOT_FOUND");
        History.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_MalformedId_Returns404_WithoutTouchingTheRepository()
    {
        var result = await Handler().HandleAsync(new UpdateGuidelineCommand("lixo", "Título válido", "d", Pillar.IDEIAS, null), default);
        result.FirstError.ToStatusCode().Should().Be(404);
        Uow.Transactions.Should().Be(0);
    }

    [Fact]
    public async Task Update_InvalidInput_Returns400_AndLeavesTheGuidelineUntouched()
    {
        var g = Seed();

        var result = await Handler().HandleAsync(new UpdateGuidelineCommand(g.Id, "x", "d", Pillar.IDEIAS, null), default);

        result.FirstError.Type.Should().Be(ErrorType.Validation);
        g.Title.Should().Be("Eficiência operacional");
        History.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_WithoutRealChanges_StillRecordsAnEntry()
    {
        var g = Seed();
        var result = await Handler().HandleAsync(new UpdateGuidelineCommand(g.Id, g.Title, g.Description, g.Pillar, g.Campaign), default);
        result.IsSuccess.Should().BeTrue();
        History.Items.Should().ContainSingle(e => e.Action == GuidelineAction.UPDATED);
    }
}

public class DeleteGuidelineHandlerTests : GuidelineTestBase
{
    private DeleteGuidelineHandler Handler() => new(Guidelines, History, Lider, Uow, Clock);

    [Fact]
    public async Task Delete_RemovesGuideline_AndKeepsTheLastStateInHistory()
    {
        var g = Seed("Sustentabilidade", "Campanha S", Pillar.PROJETOS);
        Clock.Advance(TimeSpan.FromDays(1));

        var result = await Handler().HandleAsync(new DeleteGuidelineCommand(g.Id), default);

        result.IsSuccess.Should().BeTrue();
        Guidelines.Items.Should().BeEmpty();
        var entry = History.Items.Should().ContainSingle().Subject;
        entry.Action.Should().Be(GuidelineAction.DELETED);
        entry.GuidelineId.Should().Be(g.Id, "o histórico sobrevive à exclusão");
        (entry.Category, entry.Campaign, entry.Title).Should().Be((Pillar.PROJETOS, "Campanha S", "Sustentabilidade"));
        entry.Snapshot.Title.Should().Be("Sustentabilidade");
        entry.OccurredAt.Should().Be(Clock.UtcNow);
    }

    [Fact]
    public async Task Delete_UnknownOrMalformedId_Returns404()
    {
        (await Handler().HandleAsync(new DeleteGuidelineCommand(EntityId.New()), default)).FirstError.Code.Should().Be("RESOURCE_NOT_FOUND");
        (await Handler().HandleAsync(new DeleteGuidelineCommand("lixo"), default)).FirstError.Code.Should().Be("RESOURCE_NOT_FOUND");
        History.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Twice_SecondIs404_AndOnlyOneHistoryEntry()
    {
        var g = Seed();
        await Handler().HandleAsync(new DeleteGuidelineCommand(g.Id), default);

        var second = await Handler().HandleAsync(new DeleteGuidelineCommand(g.Id), default);

        second.FirstError.ToStatusCode().Should().Be(404);
        History.Items.Should().ContainSingle();
    }
}

public class ReadGuidelinesHandlerTests : GuidelineTestBase
{
    [Fact]
    public async Task Get_ReturnsTheGuideline_Or404()
    {
        var g = Seed();
        var handler = new GetGuidelineHandler(Guidelines);

        (await handler.HandleAsync(new GetGuidelineQuery(g.Id), default)).Value.Id.Should().Be(g.Id);
        (await handler.HandleAsync(new GetGuidelineQuery(EntityId.New()), default)).FirstError.ToStatusCode().Should().Be(404);
        (await handler.HandleAsync(new GetGuidelineQuery("lixo"), default)).FirstError.ToStatusCode().Should().Be(404);
    }

    [Fact]
    public async Task List_IsNewestFirst_AndPaged()
    {
        for (var i = 0; i < 5; i++)
        {
            Clock.Advance(TimeSpan.FromMinutes(1));
            Seed($"Orientação {i}");
        }
        var handler = new ListGuidelinesHandler(TestServices.Validation(), Guidelines);

        var page = (await handler.HandleAsync(new ListGuidelinesQuery(2, 2), default)).Value;

        page.Items.Select(g => g.Title).Should().Equal("Orientação 2", "Orientação 1");
        (page.TotalItems, page.TotalPages, page.Page).Should().Be((5, 3, 2));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task List_InvalidPaging_Returns400(int page, int size)
    {
        var result = await new ListGuidelinesHandler(TestServices.Validation(), Guidelines)
            .HandleAsync(new ListGuidelinesQuery(page, size), default);
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }
}

public class GuidelineHistoryHandlerTests : GuidelineTestBase
{
    private GuidelineHistoryHandler Handler() => new(TestServices.Validation(), History);

    [Fact]
    public async Task History_MapsIdDateCategoryAndCampaign_NewestFirst()
    {
        var g = Seed("Eficiência", "Campanha A", Pillar.IDEIAS);
        History.Items.Add(GuidelineHistoryEntry.From(g, GuidelineAction.CREATED, Lider.Id, Lider.Name, Clock.UtcNow));
        g.Update("Eficiência 2", "d", Pillar.PROJETOS, "Campanha B", Clock.UtcNow.AddDays(1));
        History.Items.Add(GuidelineHistoryEntry.From(g, GuidelineAction.UPDATED, Lider.Id, Lider.Name, Clock.UtcNow.AddDays(1)));

        var page = (await Handler().HandleAsync(new GuidelineHistoryQuery(), default)).Value;

        page.Items.Select(e => e.Action).Should().Equal(GuidelineAction.UPDATED, GuidelineAction.CREATED);
        var latest = page.Items[0];
        (latest.Category, latest.Campaign, latest.OccurredAt).Should().Be((Pillar.PROJETOS, "Campanha B", Clock.UtcNow.AddDays(1)));
        latest.Id.Should().NotBeNullOrEmpty();
        page.Items[1].Snapshot.Title.Should().Be("Eficiência", "o snapshot CREATED guarda o texto original");
    }

    [Fact]
    public async Task History_PassesFiltersToTheRepository_TrimmingCampaign()
    {
        await Handler().HandleAsync(new GuidelineHistoryQuery("665f00000000000000000001", Pillar.IDEIAS, "  Campanha A ", null, null, 2, 10), default);

        History.LastQuery.Should().BeEquivalentTo(new { GuidelineId = "665f00000000000000000001", Category = Pillar.IDEIAS, Campaign = "Campanha A" });
        History.LastPage.Should().Be(new AguiaBranca.Application.Common.Paging.PageRequest(2, 10));
    }

    [Fact]
    public async Task History_DatesWithoutTimeZone_AreTreatedAsUtc()
    {
        var unspecified = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);

        await Handler().HandleAsync(new GuidelineHistoryQuery(From: unspecified), default);

        History.LastQuery!.From.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        History.LastQuery.From!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task History_FromAfterTo_Returns400()
    {
        var result = await Handler().HandleAsync(
            new GuidelineHistoryQuery(From: new DateTime(2026, 9, 2), To: new DateTime(2026, 9, 1)), default);
        result.Errors.Should().Contain(e => e.Field == "from");
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 201)]
    public async Task History_InvalidPaging_Returns400(int page, int size) =>
        (await Handler().HandleAsync(new GuidelineHistoryQuery(Page: page, PageSize: size), default))
            .FirstError.Type.Should().Be(ErrorType.Validation);

    [Fact]
    public async Task History_InvalidCategory_Returns400() =>
        (await Handler().HandleAsync(new GuidelineHistoryQuery(Category: (Pillar)77), default))
            .Errors.Should().Contain(e => e.Type == ErrorType.Validation);
}
