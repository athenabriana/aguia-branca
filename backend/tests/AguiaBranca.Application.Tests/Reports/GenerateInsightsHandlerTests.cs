using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Reports.Insights;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using NSubstitute;
using static AguiaBranca.Application.Tests.Reports.ReportTestKit;

namespace AguiaBranca.Application.Tests.Reports;

public class GenerateInsightsHandlerTests
{
    private readonly InMemoryIdeas _ideas = new();
    private readonly InMemoryProjects _projects = new();
    private readonly InMemoryGuidelines _guidelines = new();
    private readonly FakeInsightGenerator _generator = new();
    private readonly FakeInsightCache _cache = new();
    private readonly FakeInsightQuota _quota = new();
    private readonly FakeInsightPolicy _policy = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeClock _clock = new(Now);
    private readonly Guideline _guideline;
    private readonly Project _project;

    public GenerateInsightsHandlerTests()
    {
        _guideline = Guideline("Eficiência logística");
        _guidelines.Items.Add(_guideline);
        _ideas.Items.Add(Idea(Division.LOGISTICA, IdeaStatus.APROVADA, Utc(2026, 9, 1), _guideline.Id));
        _project = Project("Roteirização", guidelineId: _guideline.Id, investment: 100_000m, financialReturn: 250_000m, updatedAt: Utc(2026, 9, 10));
        _projects.Items.Add(_project);
    }

    private GenerateInsightsHandler Handler() => HandlerWith(_uow);

    internal GenerateInsightsHandler HandlerWith(IUnitOfWork uow) => new(
        TestServices.Validation(), _ideas, _projects, _guidelines, _generator, _cache, _quota, _policy,
        uow, new FakeCurrentUser { Id = "665f00000000000000000009", Role = Role.LIDER }, _clock, new FakeTimeZone());

    private Task<Result<InsightResponse>> Run(GenerateInsightsCommand? command = null, GenerateInsightsHandler? handler = null) =>
        (handler ?? Handler()).HandleAsync(command ?? new GenerateInsightsCommand(), default);

    [Fact]
    public async Task FirstCall_GeneratesAndCaches_WithMetadataAndMappedGuidelineId()
    {
        var result = await Run();

        result.IsSuccess.Should().BeTrue();
        var r = result.Value;
        r.FromCache.Should().BeFalse();
        r.Model.Should().Be("modelo-fake");
        r.GeneratedAt.Should().Be(Now);
        r.Summary.Should().Be("Resumo executivo dos resultados.");
        r.Highlights.Should().Equal("Destaque A", "Destaque B");
        r.Risks.Should().Equal("Risco A");
        r.Recommendations.Single().Should().Be(new InsightRecommendation("Escalar o piloto", "Detalhe da ação", InsightPriority.ALTA, _guideline.Id));
        _generator.Calls.Should().Be(1);
        _quota.Consumed.Should().Be(1);
        _quota.Days.Single().Should().Be("2026-09-21");
        _cache.Items.Should().ContainSingle().Which.Value.Should().Match<CachedInsight>(c =>
            c.ExpiresAt == Now.AddHours(6) && c.UserId == "665f00000000000000000009" && c.Model == "modelo-fake");
        _uow.Saves.Should().Be(1);
    }

    [Fact]
    public async Task SecondIdenticalCall_IsServedFromCache_WithoutCallingTheModelOrTheQuota()
    {
        await Run();
        _clock.Advance(TimeSpan.FromMinutes(30));

        var second = (await Run()).Value;

        second.FromCache.Should().BeTrue();
        second.GeneratedAt.Should().Be(Now, "a data de geração é a do insight original");
        second.Summary.Should().Be("Resumo executivo dos resultados.");
        second.Recommendations.Single().RelatedGuidelineId.Should().Be(_guideline.Id);
        _generator.Calls.Should().Be(1);
        _quota.Consumed.Should().Be(1);
    }

    [Fact]
    public async Task RefreshTrue_ForcesANewGeneration_AndReplacesTheCache()
    {
        await Run();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var refreshed = (await Run(new GenerateInsightsCommand(Refresh: true))).Value;

        refreshed.FromCache.Should().BeFalse();
        refreshed.GeneratedAt.Should().Be(Now.AddMinutes(5));
        _generator.Calls.Should().Be(2);
        _quota.Consumed.Should().Be(2);
        _cache.Items.Values.Single().CreatedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public async Task ExpiredCache_GeneratesAgain()
    {
        await Run();
        _clock.Advance(TimeSpan.FromHours(6).Add(TimeSpan.FromSeconds(1)));

        (await Run()).Value.FromCache.Should().BeFalse();
        _generator.Calls.Should().Be(2);
    }

    [Fact]
    public async Task ChangingTheData_ChangesTheDigest_AndMissesTheCache()
    {
        await Run();
        _projects.Items.Clear();
        _projects.Items.Add(Project("Roteirização", guidelineId: _guideline.Id, investment: 100_000m, financialReturn: 400_000m, updatedAt: Utc(2026, 9, 10)));

        (await Run()).Value.FromCache.Should().BeFalse();
        _generator.Calls.Should().Be(2);
        _cache.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task DifferentFilters_DoNotShareCacheEntries()
    {
        await Run(new GenerateInsightsCommand(Period.ALL));
        await Run(new GenerateInsightsCommand(Period.THIS_YEAR));
        await Run(new GenerateInsightsCommand(Period.ALL, Division.COMERCIO));

        _generator.Calls.Should().Be(3);
        _cache.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task DailyLimit_ReturnsRateLimitedWithRetryAfter_AndDoesNotCallTheModel()
    {
        _policy.DailyLimit = 1;
        await Run();

        var second = await Run(new GenerateInsightsCommand(Refresh: true));

        second.IsFailure.Should().BeTrue();
        second.FirstError.Should().Match<Error>(e => e.Code == "RATE_LIMITED" && e.Type == ErrorType.TooManyRequests);
        second.FirstError.ToStatusCode().Should().Be(429);
        second.FirstError.RetryAfterSeconds.Should().Be(12 * 3600, "do meio-dia local até a meia-noite local");
        _generator.Calls.Should().Be(1);
    }

    [Fact]
    public async Task DailyLimit_StillServesCachedInsights()
    {
        _policy.DailyLimit = 1;
        await Run();

        var cached = await Run();

        cached.IsSuccess.Should().BeTrue();
        cached.Value.FromCache.Should().BeTrue();
    }

    [Fact]
    public async Task ModelUnavailable_PassesTheErrorThrough_AndCachesNothing()
    {
        _generator.Behavior = _ => AiErrors.Unavailable();

        var result = await Run();

        result.FirstError.Code.Should().Be("AI_UNAVAILABLE");
        result.FirstError.ToStatusCode().Should().Be(503);
        _cache.Items.Should().BeEmpty();
        _uow.Saves.Should().Be(0);

        _generator.Behavior = null;
        (await Run()).Value.FromCache.Should().BeFalse("a falha não deixou nada em cache: a próxima tentativa gera de verdade");
    }

    [Fact]
    public async Task InvalidModelResponse_Is502_AndNothingIsCached()
    {
        _generator.Behavior = _ => AiErrors.InvalidResponse();

        var result = await Run();

        result.FirstError.Code.Should().Be("AI_INVALID_RESPONSE");
        result.FirstError.ToStatusCode().Should().Be(502);
        _cache.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task NotConfigured_IsUnavailable_WithoutConsumingQuota()
    {
        _generator.IsConfigured = false;

        var result = await Run();

        result.FirstError.Code.Should().Be("AI_UNAVAILABLE");
        _quota.Consumed.Should().Be(0);
        _generator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task GuidelineFocus_ScopesTheSummaryToThatGuideline()
    {
        var other = Guideline("Outra orientação");
        _guidelines.Items.Add(other);
        _projects.Items.Add(Project("Projeto da outra", guidelineId: other.Id, investment: 1m, financialReturn: 1m, updatedAt: Utc(2026, 9, 10)));

        await Run(new GenerateInsightsCommand(GuidelineId: _guideline.Id));

        var sent = _generator.Requests.Single().UserContent;
        sent.Should().Contain("Roteirização").And.Contain("\"orientacaoEmFoco\":\"Eficiência logística\"");
        sent.Should().NotContain("Projeto da outra").And.NotContain("Outra orientação");
    }

    [Fact]
    public async Task UnknownGuideline_Is404_AndMalformedGuideline_Is400()
    {
        var unknown = await Run(new GenerateInsightsCommand(GuidelineId: "665f0000000000000000abcd"));
        var malformed = await Run(new GenerateInsightsCommand(GuidelineId: "nao-e-id"));

        unknown.FirstError.Type.Should().Be(ErrorType.NotFound);
        malformed.FirstError.Should().Match<Error>(e => e.Type == ErrorType.Validation && e.Field == "guidelineId");
        _generator.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("G1", true)]
    [InlineData(" g1 ", true)]
    [InlineData("G9", false)]
    [InlineData("qualquer coisa", false)]
    [InlineData(null, false)]
    public async Task RelatedGuidelineRef_IsMappedToTheRealId_OrDroppedWhenUnknown(string? reference, bool mapped)
    {
        _generator.Behavior = _ => Result<GeneratedInsight>.Ok(FakeInsightGenerator.DefaultInsight(reference));

        var recommendation = (await Run()).Value.Recommendations.Single();

        recommendation.RelatedGuidelineId.Should().Be(mapped ? _guideline.Id : null);
    }

    [Fact]
    public async Task CacheDisabled_NeverStoresNorReadsTheCache()
    {
        _policy.CacheDuration = TimeSpan.Zero;

        await Run();
        var second = (await Run()).Value;

        second.FromCache.Should().BeFalse();
        _generator.Calls.Should().Be(2);
        _cache.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CorruptedCacheEntry_IsTreatedAsAMiss()
    {
        await Run();
        var entry = _cache.Items.Values.Single();
        _cache.Items[entry.CacheKey] = entry with { PayloadJson = "{isto não é json" };

        var result = await Run();

        result.Value.FromCache.Should().BeFalse();
        _generator.Calls.Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentIdenticalRequest_DuplicateKeyOnSave_DoesNotFailTheResponse()
    {
        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<Task<int>>(_ => throw new DuplicateKeyException("ux_aiInsights_cacheKey"));

        var result = await Run(handler: HandlerWith(uow));

        result.IsSuccess.Should().BeTrue();
        result.Value.FromCache.Should().BeFalse();
    }

    [Fact]
    public async Task PromptSentToTheModel_ContainsNoPersonalData()
    {
        _ideas.Items.Clear();
        _ideas.Items.Add(Domain.Entities.Idea.Create("Ideia sobre frota", "desc", "tecnologia", Division.LOGISTICA, _guideline.Id,
            "665f00000000000000000003", "Fulana de Tal", Utc(2026, 9, 2)));

        await Run();

        var sent = _generator.Requests.Single();
        (sent.SystemInstruction + sent.UserContent).Should().NotContain("Fulana").And.NotContain("665f00000000000000000003");
    }
}
