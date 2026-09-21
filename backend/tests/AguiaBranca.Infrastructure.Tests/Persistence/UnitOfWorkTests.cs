using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Tests.Persistence;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UnitOfWorkTests(MongoFixture fixture) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await fixture.CreateDatabaseAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task<long> CountAsync(string collection) =>
        await _db.Raw(collection).CountDocumentsAsync(new BsonDocument());

    [Fact]
    public async Task Transaction_Commit_PersistsWritesAcrossCollections()
    {
        var author = Builders.User(); var idea = Builders.Idea(author);
        var evt = PointEvent.Create(author.Id, 10, PointReason.IDEA_CREATED, idea.Id, Builders.Now);
        await using var ctx = _db.CreateContext();
        var uow = _db.CreateUnitOfWork(ctx);

        var result = await uow.ExecuteInTransactionAsync(async _ =>
        {
            ctx.Users.Add(author); ctx.Ideas.Add(idea); ctx.PointEvents.Add(evt);
            await Task.CompletedTask;
            return idea.Id;
        }, default);

        result.Should().Be(idea.Id);
        (await CountAsync(Collections.Users), await CountAsync(Collections.Ideas), await CountAsync(Collections.PointEvents))
            .Should().Be((1, 1, 1));
    }

    [Fact]
    public async Task Transaction_ExceptionInsideWork_RollsBackEverything()
    {
        var author = Builders.User(); var idea = Builders.Idea(author);
        await using var ctx = _db.CreateContext();
        var uow = _db.CreateUnitOfWork(ctx);

        var act = () => uow.ExecuteInTransactionAsync<int>(async ct =>
        {
            ctx.Users.Add(author);
            await ctx.SaveChangesAsync(ct); // escrita já enviada ao servidor, dentro da transação
            ctx.Ideas.Add(idea);
            throw new InvalidOperationException("falha simulada");
        }, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("falha simulada");
        (await CountAsync(Collections.Users)).Should().Be(0);
        (await CountAsync(Collections.Ideas)).Should().Be(0);
    }

    [Fact]
    public async Task Transaction_FailureOnLastWrite_RollsBackEarlierWrites()
    {
        // Duas escritas no mesmo lote: a 2ª viola o índice único parcial de originatingIdeaId.
        var ideaId = EntityId.New();
        var p1 = Builders.Project(originatingIdeaId: ideaId);
        var p2 = Builders.Project(originatingIdeaId: ideaId);
        var innocent = Builders.User();
        await using var ctx = _db.CreateContext();
        var uow = _db.CreateUnitOfWork(ctx);

        var act = () => uow.ExecuteInTransactionAsync<int>(async _ =>
        {
            ctx.Users.Add(innocent); ctx.Projects.Add(p1); ctx.Projects.Add(p2);
            await Task.CompletedTask;
            return 0;
        }, default);

        await act.Should().ThrowAsync<DuplicateKeyException>();
        (await CountAsync(Collections.Users)).Should().Be(0, "a escrita inocente também deve ser revertida");
        (await CountAsync(Collections.Projects)).Should().Be(0);
    }

    [Fact]
    public async Task DuplicateOriginatingIdea_IsTranslated_WithIndexName()
    {
        var ideaId = EntityId.New();
        await using (var seed = _db.CreateContext())
        {
            seed.Projects.Add(Builders.Project(originatingIdeaId: ideaId));
            await seed.SaveChangesAsync();
        }

        await using var ctx = _db.CreateContext();
        var uow = _db.CreateUnitOfWork(ctx);
        ctx.Projects.Add(Builders.Project(originatingIdeaId: ideaId));

        var act = () => uow.SaveChangesAsync(default);

        var ex = (await act.Should().ThrowAsync<DuplicateKeyException>()).Which;
        ex.IndexName.Should().Be("ux_projects_originatingIdeaId");
        ex.InnerException.Should().BeOfType<MongoBulkWriteException<BsonDocument>>().Which
            .Should().NotBeNull();
    }

    [Fact]
    public async Task Projects_WithoutOriginatingIdea_CanCoexist_PartialUniqueIndex()
    {
        await using var ctx = _db.CreateContext();
        ctx.Projects.AddRange(Builders.Project(), Builders.Project(), Builders.Project());
        await _db.CreateUnitOfWork(ctx).SaveChangesAsync(default);

        (await CountAsync(Collections.Projects)).Should().Be(3);
    }

    [Fact]
    public async Task StaleProjectVersion_IsTranslated_ToConcurrencyConflict()
    {
        var project = Builders.Project();
        await using (var seed = _db.CreateContext()) { seed.Projects.Add(project); await seed.SaveChangesAsync(); }

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var p1 = await first.Projects.FirstAsync(x => x.Id == project.Id);
        var p2 = await second.Projects.FirstAsync(x => x.Id == project.Id);

        p1.ApplyUpdate(Builders.ProjectData(investment: 1m), Builders.Now.AddMinutes(1));
        await _db.CreateUnitOfWork(first).SaveChangesAsync(default);

        p2.ApplyUpdate(Builders.ProjectData(investment: 2m), Builders.Now.AddMinutes(2));
        var act = () => _db.CreateUnitOfWork(second).SaveChangesAsync(default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        await using var read = _db.CreateContext();
        (await read.Projects.FirstAsync(x => x.Id == project.Id)).Investment.Should().Be(1m);
    }

    [Fact]
    public async Task NestedTransaction_ParticipatesInOuterOne()
    {
        var author = Builders.User(); var idea = Builders.Idea(author);
        await using var ctx = _db.CreateContext();
        var uow = _db.CreateUnitOfWork(ctx);

        var act = () => uow.ExecuteInTransactionAsync<int>(async ct =>
        {
            ctx.Users.Add(author);
            await uow.ExecuteInTransactionAsync(_ => { ctx.Ideas.Add(idea); return Task.FromResult(0); }, ct);
            throw new InvalidOperationException("desfaz tudo");
        }, default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await CountAsync(Collections.Users), await CountAsync(Collections.Ideas)).Should().Be((0, 0));
    }

    [Fact]
    public void Translate_ReturnsNull_ForUnknownExceptions() =>
        MongoUnitOfWork.Translate(new InvalidOperationException("x")).Should().BeNull();

    [Fact]
    public async Task SaveChanges_WithoutChanges_ReturnsZero()
    {
        await using var ctx = _db.CreateContext();
        (await _db.CreateUnitOfWork(ctx).SaveChangesAsync(default)).Should().Be(0);
    }
}
