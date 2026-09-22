using System.Security.Cryptography;
using System.Text;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Application.Tests.Support;

public sealed class FakeClock(DateTime? now = null) : IClock
{
    public DateTime UtcNow { get; set; } = now ?? new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan by) => UtcNow += by;
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Usuário";
    public Role Role { get; set; } = Role.OPERADOR;
    public Division Division { get; set; } = Division.LOGISTICA;
}

/// <summary>Executa o trabalho na hora e conta as confirmações (não há banco).</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Transactions { get; private set; }
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(0); }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        Transactions++;
        return await work(ct);
    }
}

public sealed class FakeTokenService : ITokenService
{
    private int _counter;
    public TimeSpan RefreshLifetime { get; } = TimeSpan.FromDays(7);
    public string LastAccessTokenUserId { get; private set; } = string.Empty;

    public AccessToken CreateAccessToken(AppUser user)
    {
        LastAccessTokenUserId = user.Id;
        return new AccessToken($"jwt-for-{user.Id}", DateTime.UtcNow.AddMinutes(30), 1800);
    }

    public RefreshTokenValue GenerateRefreshToken()
    {
        var raw = $"raw-{++_counter}";
        return new RefreshTokenValue(raw, HashRefreshToken(raw));
    }

    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}

public sealed class InMemoryRefreshTokens : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];

    public Task AddAsync(RefreshToken token, CancellationToken ct) { Items.Add(token); return Task.CompletedTask; }
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));
    public Task<IReadOnlyList<RefreshToken>> ListByFamilyAsync(string familyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.FamilyId == familyId).ToList());
}

public sealed class InMemoryUsers : IUserRepository
{
    public List<AppUser> Items { get; } = [];

    public Task<AppUser?> GetByIdAsync(string id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));
    public Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AppUser>>(Items.Where(u => ids.Contains(u.Id)).ToList());
    public Task<IReadOnlyList<AppUser>> ListAsync(Role? role, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AppUser>>(Items.Where(u => role is null || u.Role == role).ToList());
}

public sealed class InMemoryGuidelines : IGuidelineRepository
{
    public List<Guideline> Items { get; } = [];

    public Task<Guideline?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(g => g.Id == id));
    public Task<bool> ExistsAsync(string id, CancellationToken ct) => Task.FromResult(Items.Any(g => g.Id == id));
    public Task<IReadOnlyDictionary<string, string>> GetTitlesAsync(IEnumerable<string> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(Items.Where(g => ids.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Title));
    public Task<IReadOnlyList<Guideline>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guideline>>(Items.OrderByDescending(g => g.UpdatedAt).ToList());
    public Task<PagedResult<Guideline>> ListPagedAsync(PageRequest page, CancellationToken ct) =>
        Task.FromResult(new PagedResult<Guideline>(
            Items.OrderByDescending(g => g.UpdatedAt).Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, Items.Count));
    public Task AddAsync(Guideline guideline, CancellationToken ct) { Items.Add(guideline); return Task.CompletedTask; }
    public void Remove(Guideline guideline) => Items.Remove(guideline);
}

public sealed class InMemoryGuidelineHistory : IGuidelineHistoryRepository
{
    public List<GuidelineHistoryEntry> Items { get; } = [];
    public GuidelineHistoryQuery? LastQuery { get; private set; }
    public PageRequest? LastPage { get; private set; }

    public Task AddAsync(GuidelineHistoryEntry entry, CancellationToken ct) { Items.Add(entry); return Task.CompletedTask; }

    public Task<PagedResult<GuidelineHistoryEntry>> QueryAsync(GuidelineHistoryQuery query, PageRequest page, CancellationToken ct)
    {
        LastQuery = query; LastPage = page;
        var filtered = Items.AsEnumerable();
        if (query.GuidelineId is { } g) filtered = filtered.Where(e => e.GuidelineId == g);
        if (query.Category is { } c) filtered = filtered.Where(e => e.Category == c);
        if (query.Campaign is { } camp) filtered = filtered.Where(e => e.Campaign == camp);
        if (query.From is { } f) filtered = filtered.Where(e => e.OccurredAt >= f);
        if (query.To is { } t) filtered = filtered.Where(e => e.OccurredAt <= t);
        var list = filtered.OrderByDescending(e => e.OccurredAt).ToList();
        return Task.FromResult(new PagedResult<GuidelineHistoryEntry>(list.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, list.Count));
    }
}

public sealed class FakeTimeZone(string id = "America/Sao_Paulo") : ITimeZoneProvider
{
    public TimeZoneInfo ReportTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(id);
}

public sealed class InMemoryPointEvents : IPointEventRepository
{
    public List<PointEvent> Items { get; } = [];
    public Task AddAsync(PointEvent pointEvent, CancellationToken ct) { Items.Add(pointEvent); return Task.CompletedTask; }
    public Task<IReadOnlyList<PointEvent>> ListBetweenAsync(DateTime fromInclusive, DateTime toExclusive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PointEvent>>(Items.Where(e => e.CreatedAt >= fromInclusive && e.CreatedAt < toExclusive).ToList());
}

public sealed class InMemoryIdeas : IIdeaRepository
{
    public List<Idea> Items { get; } = [];
    public IdeaQuery? LastQuery { get; private set; }

    public Task<Idea?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task AddAsync(Idea idea, CancellationToken ct) { Items.Add(idea); return Task.CompletedTask; }
    public void Remove(Idea idea) => Items.Remove(idea);

    public Task<PagedResult<Idea>> QueryAsync(IdeaQuery query, PageRequest page, CancellationToken ct)
    {
        LastQuery = query;
        var q = Items.AsEnumerable();
        if (query.AuthorId is { } a) q = q.Where(i => i.AuthorId == a);
        if (query.Statuses is { Count: > 0 } st) q = q.Where(i => st.Contains(i.Status));
        if (query.GuidelineId is { } g) q = q.Where(i => i.GuidelineId == g);
        if (query.Division is { } d) q = q.Where(i => i.Division == d);
        var list = (query.Sort == IdeaSort.ICE_SCORE_DESC
            ? q.OrderByDescending(i => i.Ice?.Score ?? -1).ThenByDescending(i => i.CreatedAt)
            : q.OrderByDescending(i => i.CreatedAt)).ToList();
        return Task.FromResult(new PagedResult<Idea>(list.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, list.Count));
    }

    public Task<IReadOnlyList<Idea>> ListByAuthorAsync(string authorId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Idea>>(Items.Where(i => i.AuthorId == authorId).ToList());
    public Task<IReadOnlyList<Idea>> ListAllAsync(Division? division, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Idea>>(Items.Where(i => division is null || i.Division == division).ToList());
}

public sealed class InMemoryProjects : IProjectRepository
{
    public List<Project> Items { get; } = [];

    public Task<Project?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
    public Task<Project?> GetByOriginatingIdeaIdAsync(string ideaId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(p => p.OriginatingIdeaId == ideaId));
    public Task<IReadOnlyList<Project>> GetByOriginatingIdeaIdsAsync(IEnumerable<string> ideaIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Project>>(Items.Where(p => p.OriginatingIdeaId != null && ideaIds.Contains(p.OriginatingIdeaId)).ToList());
    public Task AddAsync(Project project, CancellationToken ct) { Items.Add(project); return Task.CompletedTask; }
    public void Remove(Project project) => Items.Remove(project);
    public Task<PagedResult<Project>> QueryAsync(ProjectQuery query, PageRequest page, CancellationToken ct) =>
        Task.FromResult(new PagedResult<Project>(Items.OrderByDescending(p => p.UpdatedAt).Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, Items.Count));
    public Task<IReadOnlyList<Project>> ListAllAsync(Division? division, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Project>>(Items.Where(p => division is null || p.Division == division).ToList());
}

public sealed class InMemoryProjectUpdates : IProjectUpdateRepository
{
    public List<ProjectUpdate> Items { get; } = [];
    public Task AddAsync(ProjectUpdate update, CancellationToken ct) { Items.Add(update); return Task.CompletedTask; }
    public Task<PagedResult<ProjectUpdate>> ListByProjectAsync(string projectId, PageRequest page, CancellationToken ct)
    {
        var list = Items.Where(u => u.ProjectId == projectId).OrderByDescending(u => u.CreatedAt).ToList();
        return Task.FromResult(new PagedResult<ProjectUpdate>(list.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, list.Count));
    }
    public Task RemoveByProjectAsync(string projectId, CancellationToken ct) { Items.RemoveAll(u => u.ProjectId == projectId); return Task.CompletedTask; }
}

public sealed class FakeIdentityService : IIdentityService
{
    public CredentialCheckResult Result { get; set; } = CredentialCheckResult.Invalid();
    public List<(string Email, string Password)> Calls { get; } = [];

    public Task<CredentialCheckResult> CheckCredentialsAsync(string email, string password, CancellationToken ct)
    {
        Calls.Add((email, password));
        return Task.FromResult(Result);
    }

    public Task<Application.Common.Results.Result<AppUser>> CreateUserAsync(AppUser user, string password, CancellationToken ct) =>
        throw new NotSupportedException();
}

internal static class TestServices
{
    /// <summary>Serviço de validação real (com todos os validators da Application).</summary>
    public static IValidationService Validation()
    {
        var provider = new ServiceCollection().AddApplication().BuildServiceProvider();
        return provider.CreateScope().ServiceProvider.GetRequiredService<IValidationService>();
    }
}

internal static class UserFactory
{
    public static AppUser Operator(string name = "Operador") =>
        AppUser.Create(name, $"{Guid.NewGuid():N}@aguiabranca.com", Role.OPERADOR, Division.LOGISTICA, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
}
