using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Domain.Tests;

public class ProjectTests
{
    [Fact]
    public void Create_DefaultsResponsibleToCreator_AndStartsAtVersion1()
    {
        var p = TestData.NewProject();
        p.ResponsibleId.Should().Be(TestData.ReviewerId);
        p.ResponsibleName.Should().Be("Gestor");
        p.Version.Should().Be(1);
        p.OriginatingIdeaId.Should().BeNull();
    }

    [Fact]
    public void Create_NegativeAmounts_Throw()
    {
        var data = TestData.ProjectData() with { Investment = -1m };
        FluentActions.Invoking(() => TestData.NewProject(data)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_BlankTitle_Throws() =>
        FluentActions.Invoking(() => TestData.NewProject(TestData.ProjectData() with { Title = " " }))
            .Should().Throw<DomainException>();

    [Fact]
    public void DraftFromIdea_InheritsFieldsFromIdea()
    {
        var author = TestData.User();
        var idea = TestData.NewIdea(author, guidelineId: TestData.ValidGuidelineId);
        idea.SaveIce(new Ice(8, 7, 6), TestData.ReviewerId, TestData.Now);

        var p = Project.CreateDraftFromIdea(idea, TestData.ReviewerId, "Gestor", TestData.Now);

        p.Title.Should().Be("PROJ: Roteirização com IA");
        p.Stage.Should().Be(ProjectStage.PLANEJAMENTO);
        p.OriginatingIdeaId.Should().Be(idea.Id);
        p.GuidelineId.Should().Be(TestData.ValidGuidelineId);
        p.Division.Should().Be(idea.Division);
        p.CreatorManagerId.Should().Be(TestData.ReviewerId);
        p.PriorityScore.Should().Be(336);
        p.ReporterId.Should().Be(author.Id);
        p.ReporterName.Should().Be(author.Name);
        p.ResponsibleId.Should().Be(TestData.ReviewerId);
        p.Investment.Should().Be(0m);
    }

    [Fact]
    public void ApplyUpdate_InvestmentChange_ProducesExactlyOneNumericChange()
    {
        var p = TestData.NewProject(TestData.ProjectData(investment: 100m));

        var outcome = p.ApplyUpdate(TestData.ProjectData(investment: 120m), TestData.Now.AddDays(1));

        outcome.Changes.Should().ContainSingle();
        var change = outcome.Changes[0];
        change.Field.Should().Be("investment");
        change.Kind.Should().Be(FieldValueKind.NUMBER);
        (change.From, change.To).Should().Be(("100", "120"));
        p.Investment.Should().Be(120m);
        p.Version.Should().Be(2);
        p.UpdatedAt.Should().Be(TestData.Now.AddDays(1));
    }

    [Fact]
    public void ApplyUpdate_NoRealChange_HasEmptyDiff_ButStillBumpsVersion()
    {
        var p = TestData.NewProject();

        var outcome = p.ApplyUpdate(TestData.ProjectData(), TestData.Now.AddHours(1));

        outcome.Changes.Should().BeEmpty();
        outcome.BecameCompleted.Should().BeFalse();
        p.Version.Should().Be(2);
    }

    [Fact]
    public void ApplyUpdate_MultipleFields_ReportsOnlyChangedOnes()
    {
        var p = TestData.NewProject();
        var data = TestData.ProjectData(ProjectStage.EM_EXECUCAO) with
        {
            Title = "Projeto Y", TargetDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), FinancialReturn = 5m
        };

        var changes = p.ApplyUpdate(data, TestData.Now).Changes;

        changes.Select(c => c.Field).Should().BeEquivalentTo(["title", "stage", "financialReturn", "targetDate"]);
        changes.Single(c => c.Field == "targetDate").Kind.Should().Be(FieldValueKind.DATE);
        changes.Single(c => c.Field == "targetDate").To.Should().Be("2026-12-01T00:00:00Z");
        changes.Single(c => c.Field == "stage").To.Should().Be("EM_EXECUCAO");
    }

    [Fact]
    public void ApplyUpdate_ResponsibleChange_IsReported()
    {
        var p = TestData.NewProject();
        var data = TestData.ProjectData() with { ResponsibleId = "665f00000000000000000077", ResponsibleName = "Outro" };

        var changes = p.ApplyUpdate(data, TestData.Now).Changes;

        changes.Should().ContainSingle(c => c.Field == "responsável" && c.From == "Gestor" && c.To == "Outro");
        p.ResponsibleName.Should().Be("Outro");
    }

    [Fact]
    public void ApplyUpdate_WithoutResponsible_KeepsCurrentOne()
    {
        var p = TestData.NewProject();
        p.ApplyUpdate(TestData.ProjectData(), TestData.Now);
        p.ResponsibleId.Should().Be(TestData.ReviewerId);
    }

    [Fact]
    public void ApplyUpdate_ToCompleted_FlagsCompletion_OnlyOnTransition()
    {
        var p = TestData.NewProject();

        p.ApplyUpdate(TestData.ProjectData(ProjectStage.CONCLUIDO), TestData.Now).BecameCompleted.Should().BeTrue();
        p.ApplyUpdate(TestData.ProjectData(ProjectStage.CONCLUIDO), TestData.Now).BecameCompleted.Should().BeFalse();
    }

    [Fact]
    public void ApplyUpdate_StaleVersion_ThrowsConcurrencyConflict()
    {
        var p = TestData.NewProject();
        p.ApplyUpdate(TestData.ProjectData(investment: 1m), TestData.Now); // versão vai para 2

        var act = () => p.ApplyUpdate(TestData.ProjectData(investment: 2m), TestData.Now, expectedVersion: 1);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.ConcurrencyConflict);
        p.Investment.Should().Be(1m); // nada foi aplicado
    }

    [Fact]
    public void ApplyUpdate_MatchingVersion_Succeeds()
    {
        var p = TestData.NewProject();
        p.ApplyUpdate(TestData.ProjectData(investment: 2m), TestData.Now, expectedVersion: 1);
        p.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(100, 250, 150.0)]
    [InlineData(200, 100, -50.0)]
    public void Roi_IsNetOverInvestment(int investment, int ret, double expected)
    {
        var p = TestData.NewProject(TestData.ProjectData(investment: investment, financialReturn: ret));
        p.RoiPercent.Should().Be((decimal)expected);
        p.NetProfit.Should().Be(ret - investment);
    }

    [Fact]
    public void Roi_WithZeroInvestment_IsNull() =>
        TestData.NewProject(TestData.ProjectData(investment: 0m, financialReturn: 50m)).RoiPercent.Should().BeNull();

    private static readonly DateOnly Today = new(2026, 9, 21);

    [Theory]
    [InlineData(ProjectStage.EM_EXECUCAO, -1, true)]
    [InlineData(ProjectStage.EM_EXECUCAO, 0, false)]
    [InlineData(ProjectStage.EM_EXECUCAO, 1, false)]
    [InlineData(ProjectStage.PLANEJAMENTO, -30, true)]
    [InlineData(ProjectStage.CONCLUIDO, -1, false)]
    [InlineData(ProjectStage.CANCELADO, -1, false)]
    public void IsOverdue_OnlyWhenOpenAndDeadlineDayHasPassed(ProjectStage stage, int daysFromToday, bool expected)
    {
        var data = TestData.ProjectData(stage) with { TargetDate = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc).AddDays(daysFromToday) };
        TestData.NewProject(data).IsOverdue(Today).Should().Be(expected);
    }

    [Fact]
    public void IsOverdue_WithoutDeadline_IsFalse() =>
        TestData.NewProject().IsOverdue(Today).Should().BeFalse();

    [Theory]
    [InlineData(ProjectStage.EM_EXECUCAO, 10, 10)]
    [InlineData(ProjectStage.EM_EXECUCAO, -3, -3)]
    [InlineData(ProjectStage.PLANEJAMENTO, 0, 0)]
    public void DaysToDeadline_CountsCalendarDays(ProjectStage stage, int daysFromToday, int expected)
    {
        var data = TestData.ProjectData(stage) with { TargetDate = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc).AddDays(daysFromToday) };
        TestData.NewProject(data).DaysToDeadline(Today).Should().Be(expected);
    }

    [Theory]
    [InlineData(ProjectStage.CONCLUIDO)]
    [InlineData(ProjectStage.CANCELADO)]
    public void DaysToDeadline_IsNullForFinishedProjectsAndWithoutDeadline(ProjectStage stage)
    {
        var withDate = TestData.ProjectData(stage) with { TargetDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) };
        TestData.NewProject(withDate).DaysToDeadline(Today).Should().BeNull();
        TestData.NewProject(TestData.ProjectData(ProjectStage.EM_EXECUCAO)).DaysToDeadline(Today).Should().BeNull();
    }

    [Fact]
    public void ProjectUpdate_Create_TrimsNoteAndKeepsChanges()
    {
        var changes = new[] { FieldChange.Number("investment", 100m, 120m) };
        var update = ProjectUpdate.Create("665f00000000000000000003", TestData.ReviewerId, "Gestor", "  ajuste  ", changes, TestData.Now);
        update.Note.Should().Be("ajuste");
        update.Changes.Should().HaveCount(1);
    }

    [Fact]
    public void FieldChange_NormalizesNumberFormatting()
    {
        FieldChange.FormatNumber(100.00m).Should().Be("100");
        FieldChange.FormatNumber(12.50m).Should().Be("12.5");
        FieldChange.FormatNumber(0m).Should().Be("0");
    }
}
