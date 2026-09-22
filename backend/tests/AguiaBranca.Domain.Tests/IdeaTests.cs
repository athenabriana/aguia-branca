using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Domain.Tests;

public class IdeaTests
{
    private const string Reviewer = TestData.ReviewerId;

    [Fact]
    public void Create_StartsSubmitted_AndNormalizesCategory()
    {
        var idea = TestData.NewIdea();
        idea.Status.Should().Be(IdeaStatus.SUBMETIDA);
        idea.Category.Should().Be("Tecnologia");
        idea.Ice.Should().BeNull();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("  ")]
    public void Category_TooShort_Throws(string category) =>
        FluentActions.Invoking(() => Idea.NormalizeCategory(category)).Should().Throw<DomainException>();

    [Fact]
    public void Category_TooLong_Throws() =>
        FluentActions.Invoking(() => Idea.NormalizeCategory(new string('x', 41))).Should().Throw<DomainException>();

    [Fact]
    public void Create_InvalidGuidelineId_Throws() =>
        FluentActions.Invoking(() => TestData.NewIdea(guidelineId: "nao-e-objectid")).Should().Throw<DomainException>();

    [Fact]
    public void CreationPoints_Depend_OnStrategicLink()
    {
        TestData.NewIdea().CreationPoints.Should().Be(10);
        TestData.NewIdea(guidelineId: TestData.ValidGuidelineId).CreationPoints.Should().Be(15);
    }

    [Fact]
    public void EditContent_WhenSubmitted_Works()
    {
        var idea = TestData.NewIdea();
        idea.EditContent("Título novo", "d", "operações", Division.COMERCIO, null, TestData.Now.AddMinutes(1));
        idea.Title.Should().Be("Título novo");
        idea.Category.Should().Be("Operações");
        idea.Division.Should().Be(Division.COMERCIO);
    }

    [Theory]
    [InlineData(IdeaStatus.EM_ANALISE)]
    [InlineData(IdeaStatus.APROVADA)]
    [InlineData(IdeaStatus.REJEITADA)]
    [InlineData(IdeaStatus.IMPLEMENTADA)]
    public void EditContent_AfterSubmitted_IsNotEditable(IdeaStatus status)
    {
        var idea = TestData.IdeaWithStatus(status);
        var act = () => idea.EditContent("Outro título", "d", "categoria", Division.LOGISTICA, null, TestData.Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.IdeaNotEditable);
        idea.Invoking(i => i.EnsureDeletable()).Should().Throw<DomainException>()
            .Which.Code.Should().Be(DomainErrorCodes.IdeaNotEditable);
    }

    [Fact]
    public void SaveIce_FromSubmitted_MovesToEmAnalise()
    {
        var idea = TestData.NewIdea();
        idea.SaveIce(new Ice(8, 7, 6), Reviewer, TestData.Now);
        idea.Status.Should().Be(IdeaStatus.EM_ANALISE);
        idea.Ice!.Score.Should().Be(336);
        idea.ReviewerId.Should().Be(Reviewer);
    }

    [Fact]
    public void SaveIce_Again_KeepsEmAnalise_AndReplacesIce()
    {
        var idea = TestData.IdeaWithStatus(IdeaStatus.EM_ANALISE);
        idea.SaveIce(new Ice(10, 10, 10), Reviewer, TestData.Now);
        idea.Status.Should().Be(IdeaStatus.EM_ANALISE);
        idea.Ice!.Score.Should().Be(1000);
    }

    [Theory]
    [InlineData(IdeaStatus.APROVADA)]
    [InlineData(IdeaStatus.REJEITADA)]
    [InlineData(IdeaStatus.IMPLEMENTADA)]
    public void SaveIce_OnClosedIdea_Throws(IdeaStatus status)
    {
        var idea = TestData.IdeaWithStatus(status);
        var act = () => idea.SaveIce(new Ice(1, 1, 1), Reviewer, TestData.Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.IdeaInvalidState);
    }

    [Theory]
    [InlineData(IdeaStatus.SUBMETIDA)]
    [InlineData(IdeaStatus.EM_ANALISE)]
    public void Approve_FromOpen_ChangesStateAndReturnsTrue(IdeaStatus from)
    {
        var idea = TestData.IdeaWithStatus(from);
        var changed = idea.Approve(Reviewer, TestData.Now.AddMinutes(5));
        changed.Should().BeTrue();
        idea.Status.Should().Be(IdeaStatus.APROVADA);
        idea.ReviewedAt.Should().Be(TestData.Now.AddMinutes(5));
    }

    [Theory]
    [InlineData(IdeaStatus.APROVADA)]
    [InlineData(IdeaStatus.IMPLEMENTADA)]
    public void Approve_WhenAlreadyApproved_IsIdempotent(IdeaStatus status)
    {
        var idea = TestData.IdeaWithStatus(status);
        idea.Approve(Reviewer, TestData.Now).Should().BeFalse();
        idea.Status.Should().Be(status);
    }

    [Fact]
    public void Approve_ByAuthor_IsForbidden_EvenIfAlreadyApproved()
    {
        var author = TestData.User();
        var idea = TestData.NewIdea(author);

        var act = () => idea.Approve(author.Id, TestData.Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.SelfApprovalForbidden);
        idea.Status.Should().Be(IdeaStatus.SUBMETIDA);
    }

    [Fact]
    public void Approve_Rejected_Throws()
    {
        var idea = TestData.IdeaWithStatus(IdeaStatus.REJEITADA);
        idea.Invoking(i => i.Approve(Reviewer, TestData.Now)).Should().Throw<DomainException>()
            .Which.Code.Should().Be(DomainErrorCodes.IdeaInvalidState);
    }

    [Fact]
    public void Reject_WithComment_SetsFields()
    {
        var idea = TestData.NewIdea();
        idea.Reject(Reviewer, "  Fora do escopo  ", TestData.Now);
        idea.Status.Should().Be(IdeaStatus.REJEITADA);
        idea.ReviewComment.Should().Be("Fora do escopo");
        idea.ReviewedAt.Should().Be(TestData.Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutComment_Throws(string comment)
    {
        var idea = TestData.NewIdea();
        var act = () => idea.Reject(Reviewer, comment, TestData.Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.ValidationError);
        idea.Status.Should().Be(IdeaStatus.SUBMETIDA);
    }

    [Fact]
    public void Reject_AfterApproval_Throws() =>
        TestData.IdeaWithStatus(IdeaStatus.APROVADA)
            .Invoking(i => i.Reject(Reviewer, "tarde demais", TestData.Now))
            .Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.IdeaInvalidState);

    [Fact]
    public void MarkImplemented_FromApproved_ReturnsTrueOnce()
    {
        var idea = TestData.IdeaWithStatus(IdeaStatus.APROVADA);
        idea.MarkImplemented(TestData.Now).Should().BeTrue();
        idea.Status.Should().Be(IdeaStatus.IMPLEMENTADA);
        idea.MarkImplemented(TestData.Now).Should().BeFalse();
    }

    [Theory]
    [InlineData(IdeaStatus.SUBMETIDA)]
    [InlineData(IdeaStatus.EM_ANALISE)]
    [InlineData(IdeaStatus.REJEITADA)]
    public void MarkImplemented_FromNonApproved_IsNoOp(IdeaStatus status)
    {
        var idea = TestData.IdeaWithStatus(status);
        idea.MarkImplemented(TestData.Now).Should().BeFalse();
        idea.Status.Should().Be(status);
    }
}
