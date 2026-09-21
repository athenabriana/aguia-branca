using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Ok_ExposesValue()
    {
        var r = Result.Ok(42);
        r.IsSuccess.Should().BeTrue();
        r.IsFailure.Should().BeFalse();
        r.Value.Should().Be(42);
        r.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Fail_ExposesErrors_AndValueThrows()
    {
        Result<int> r = Error.NotFound("não achei");

        r.IsFailure.Should().BeTrue();
        r.FirstError.Code.Should().Be("RESOURCE_NOT_FOUND");
        r.Invoking(x => x.Value).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_WithNoErrors_IsRejected() =>
        FluentActions.Invoking(() => Result<int>.Fail(Array.Empty<Error>())).Should().Throw<ArgumentException>();

    [Fact]
    public void Ok_Unit_HasNoPayload() => Result.Ok().IsSuccess.Should().BeTrue();

    [Fact]
    public void Map_TransformsSuccess_AndPropagatesFailure()
    {
        Result.Ok(2).Map(x => x * 10).Value.Should().Be(20);

        var failed = Result.Fail<int>(Error.Forbidden("não"));
        failed.Map(x => x * 10).FirstError.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public void Match_PicksBranch()
    {
        Result.Ok(1).Match(v => $"ok:{v}", e => "fail").Should().Be("ok:1");
        Result.Fail<int>(Error.Conflict("X", "y")).Match(v => "ok", e => $"fail:{e[0].Code}").Should().Be("fail:X");
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unprocessable, 422)]
    [InlineData(ErrorType.TooManyRequests, 429)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void ErrorType_MapsToHttpStatus(ErrorType type, int status) =>
        new Error("C", "m", type).ToStatusCode().Should().Be(status);

    [Theory]
    [InlineData("AI_INVALID_RESPONSE", 502)]
    [InlineData("AI_UNAVAILABLE", 503)]
    public void ExternalErrors_MapByCode(string code, int status) =>
        Error.External(code, "m").ToStatusCode().Should().Be(status);
}

public class DomainErrorMapperTests
{
    [Fact]
    public void Validation_KeepsField()
    {
        var e = DomainException.Validation("title", "curto").ToError();
        (e.Type, e.Code, e.Field).Should().Be((ErrorType.Validation, "VALIDATION_ERROR", "title"));
    }

    [Theory]
    [InlineData(DomainErrorCodes.IdeaNotEditable)]
    [InlineData(DomainErrorCodes.IdeaInvalidState)]
    [InlineData(DomainErrorCodes.ConcurrencyConflict)]
    public void StateConflicts_AreConflict(string code) =>
        new DomainException(code, "m").ToError().Type.Should().Be(ErrorType.Conflict);

    [Fact]
    public void SelfApproval_IsForbidden_WithCode()
    {
        var e = new DomainException(DomainErrorCodes.SelfApprovalForbidden, "não").ToError();
        (e.Type, e.Code).Should().Be((ErrorType.Forbidden, "SELF_APPROVAL_FORBIDDEN"));
    }

    [Fact]
    public void UnknownDomainCode_IsUnprocessable() =>
        new DomainException("OUTRA", "m").ToError().Type.Should().Be(ErrorType.Unprocessable);

    [Fact]
    public void ToResult_WrapsFailure() =>
        new DomainException("OUTRA", "m").ToResult<int>().IsFailure.Should().BeTrue();
}
