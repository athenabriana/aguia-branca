using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AguiaBranca.Infrastructure.Persistence.Repositories;

// Convenção: métodos GetBy* devolvem entidades RASTREADAS (para alteração); listas/consultas são AsNoTracking.
// Ids que não são ObjectId nunca chegam ao banco (o conversor lançaria FormatException): viram "não encontrado".

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<AppUser?> GetByIdAsync(string id, CancellationToken ct) =>
        EntityId.IsValid(id) ? await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct) : null;

    public async Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var valid = ids.Where(EntityId.IsValid).Distinct().ToArray();
        return valid.Length == 0 ? [] : await db.Users.Where(x => valid.Contains(x.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AppUser>> ListAsync(Role? role, CancellationToken ct)
    {
        IQueryable<AppUser> query = db.Users.AsNoTracking();
        if (role is { } r) query = query.Where(x => x.Role == r);
        return (await query.ToListAsync(ct)).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

internal sealed class GuidelineRepository(AppDbContext db) : IGuidelineRepository
{
    public async Task<Guideline?> GetByIdAsync(string id, CancellationToken ct) =>
        EntityId.IsValid(id) ? await db.Guidelines.FirstOrDefaultAsync(x => x.Id == id, ct) : null;

    public async Task<bool> ExistsAsync(string id, CancellationToken ct) =>
        EntityId.IsValid(id) && await db.Guidelines.AsNoTracking().AnyAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyDictionary<string, string>> GetTitlesAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var valid = ids.Where(EntityId.IsValid).Distinct().ToArray();
        if (valid.Length == 0) return new Dictionary<string, string>();

        var items = await db.Guidelines.AsNoTracking().Where(x => valid.Contains(x.Id)).ToListAsync(ct);
        return items.ToDictionary(x => x.Id, x => x.Title);
    }

    public async Task<IReadOnlyList<Guideline>> ListAsync(CancellationToken ct) =>
        await db.Guidelines.AsNoTracking().OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);

    public async Task<PagedResult<Guideline>> ListPagedAsync(PageRequest page, CancellationToken ct)
    {
        var total = await db.Guidelines.AsNoTracking().CountAsync(ct);
        var items = await db.Guidelines.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<Guideline>(items, page.Page, page.PageSize, total);
    }

    public async Task AddAsync(Guideline guideline, CancellationToken ct) => await db.Guidelines.AddAsync(guideline, ct);
    public void Remove(Guideline guideline) => db.Guidelines.Remove(guideline);
}

internal sealed class GuidelineHistoryRepository(AppDbContext db) : IGuidelineHistoryRepository
{
    public async Task AddAsync(GuidelineHistoryEntry entry, CancellationToken ct) =>
        await db.GuidelineHistory.AddAsync(entry, ct);

    public async Task<PagedResult<GuidelineHistoryEntry>> QueryAsync(GuidelineHistoryQuery query, PageRequest page, CancellationToken ct)
    {
        if (query.GuidelineId is not null && !EntityId.IsValid(query.GuidelineId)) return PagedResult<GuidelineHistoryEntry>.Empty(page);

        IQueryable<GuidelineHistoryEntry> q = db.GuidelineHistory.AsNoTracking();
        if (query.GuidelineId is { } gid) q = q.Where(x => x.GuidelineId == gid);
        if (query.Category is { } cat) q = q.Where(x => x.Category == cat);
        if (query.Campaign is { } campaign) q = q.Where(x => x.Campaign == campaign);
        if (query.From is { } from) q = q.Where(x => x.OccurredAt >= from);
        if (query.To is { } to) q = q.Where(x => x.OccurredAt <= to);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.OccurredAt).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<GuidelineHistoryEntry>(items, page.Page, page.PageSize, total);
    }
}

internal sealed class IdeaRepository(AppDbContext db) : IIdeaRepository
{
    public async Task<Idea?> GetByIdAsync(string id, CancellationToken ct) =>
        EntityId.IsValid(id) ? await db.Ideas.FirstOrDefaultAsync(x => x.Id == id, ct) : null;

    public async Task AddAsync(Idea idea, CancellationToken ct) => await db.Ideas.AddAsync(idea, ct);
    public void Remove(Idea idea) => db.Ideas.Remove(idea);

    public async Task<PagedResult<Idea>> QueryAsync(IdeaQuery query, PageRequest page, CancellationToken ct)
    {
        if ((query.AuthorId is not null && !EntityId.IsValid(query.AuthorId)) ||
            (query.GuidelineId is not null && !EntityId.IsValid(query.GuidelineId)))
            return PagedResult<Idea>.Empty(page);

        IQueryable<Idea> q = db.Ideas.AsNoTracking();
        if (query.AuthorId is { } author) q = q.Where(x => x.AuthorId == author);
        if (query.GuidelineId is { } guideline) q = q.Where(x => x.GuidelineId == guideline);
        if (query.Division is { } division) q = q.Where(x => x.Division == division);
        if (query.Statuses is { Count: > 0 } statuses)
        {
            var list = statuses.ToArray();
            q = q.Where(x => list.Contains(x.Status));
        }

        var total = await q.CountAsync(ct);
        var ordered = query.Sort == IdeaSort.ICE_SCORE_DESC
            // sem ICE (null) vai ao fim: o Mongo ordena null como o menor valor
            ? q.OrderByDescending(x => x.Ice!.Score).ThenByDescending(x => x.CreatedAt)
            : q.OrderByDescending(x => x.CreatedAt);

        var items = await ordered.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<Idea>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<Idea>> ListByAuthorAsync(string authorId, CancellationToken ct) =>
        EntityId.IsValid(authorId)
            ? await db.Ideas.AsNoTracking().Where(x => x.AuthorId == authorId).ToListAsync(ct)
            : [];

    public async Task<IReadOnlyList<Idea>> ListAllAsync(Division? division, CancellationToken ct)
    {
        IQueryable<Idea> q = db.Ideas.AsNoTracking();
        if (division is { } d) q = q.Where(x => x.Division == d);
        return await q.ToListAsync(ct);
    }
}

internal sealed class ProjectRepository(AppDbContext db) : IProjectRepository
{
    public async Task<Project?> GetByIdAsync(string id, CancellationToken ct) =>
        EntityId.IsValid(id) ? await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct) : null;

    public async Task<Project?> GetByOriginatingIdeaIdAsync(string ideaId, CancellationToken ct) =>
        EntityId.IsValid(ideaId) ? await db.Projects.FirstOrDefaultAsync(x => x.OriginatingIdeaId == ideaId, ct) : null;

    public async Task<IReadOnlyList<Project>> GetByOriginatingIdeaIdsAsync(IEnumerable<string> ideaIds, CancellationToken ct)
    {
        var valid = ideaIds.Where(EntityId.IsValid).Distinct().ToArray();
        return valid.Length == 0
            ? []
            : await db.Projects.AsNoTracking().Where(x => x.OriginatingIdeaId != null && valid.Contains(x.OriginatingIdeaId)).ToListAsync(ct);
    }

    public async Task AddAsync(Project project, CancellationToken ct) => await db.Projects.AddAsync(project, ct);
    public void Remove(Project project) => db.Projects.Remove(project);

    public async Task<PagedResult<Project>> QueryAsync(ProjectQuery query, PageRequest page, CancellationToken ct)
    {
        if (query.GuidelineId is not null && !EntityId.IsValid(query.GuidelineId)) return PagedResult<Project>.Empty(page);

        IQueryable<Project> q = db.Projects.AsNoTracking();
        if (query.Stage is { } stage) q = q.Where(x => x.Stage == stage);
        if (query.Division is { } division) q = q.Where(x => x.Division == division);
        if (query.GuidelineId is { } guideline) q = q.Where(x => x.GuidelineId == guideline);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.UpdatedAt).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<Project>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<Project>> ListAllAsync(Division? division, CancellationToken ct)
    {
        IQueryable<Project> q = db.Projects.AsNoTracking();
        if (division is { } d) q = q.Where(x => x.Division == d);
        return await q.ToListAsync(ct);
    }
}

internal sealed class ProjectUpdateRepository(AppDbContext db) : IProjectUpdateRepository
{
    public async Task AddAsync(ProjectUpdate update, CancellationToken ct) => await db.ProjectUpdates.AddAsync(update, ct);

    public async Task<PagedResult<ProjectUpdate>> ListByProjectAsync(string projectId, PageRequest page, CancellationToken ct)
    {
        if (!EntityId.IsValid(projectId)) return PagedResult<ProjectUpdate>.Empty(page);

        var q = db.ProjectUpdates.AsNoTracking().Where(x => x.ProjectId == projectId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<ProjectUpdate>(items, page.Page, page.PageSize, total);
    }

    public async Task RemoveByProjectAsync(string projectId, CancellationToken ct)
    {
        if (!EntityId.IsValid(projectId)) return;
        var items = await db.ProjectUpdates.Where(x => x.ProjectId == projectId).ToListAsync(ct);
        db.ProjectUpdates.RemoveRange(items);
    }
}

internal sealed class PointEventRepository(AppDbContext db) : IPointEventRepository
{
    public async Task AddAsync(PointEvent pointEvent, CancellationToken ct) => await db.PointEvents.AddAsync(pointEvent, ct);

    public async Task<IReadOnlyList<PointEvent>> ListBetweenAsync(DateTime fromInclusive, DateTime toExclusive, CancellationToken ct) =>
        await db.PointEvents.AsNoTracking()
            .Where(x => x.CreatedAt >= fromInclusive && x.CreatedAt < toExclusive)
            .ToListAsync(ct);
}

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct) => await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> ListByFamilyAsync(string familyId, CancellationToken ct) =>
        EntityId.IsValid(familyId) ? await db.RefreshTokens.Where(x => x.FamilyId == familyId).ToListAsync(ct) : [];
}
