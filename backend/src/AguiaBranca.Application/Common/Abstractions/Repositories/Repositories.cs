using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Common.Abstractions.Repositories;

// Convenções: métodos GetBy* devolvem entidades RASTREADAS (alterações persistem via IUnitOfWork); listas e consultas
// (List*/Query*) são somente leitura (AsNoTracking) — para alterar, carregue com GetBy*.
// Ids inválidos (não ObjectId) devem ser filtrados antes, com EntityId.IsValid.

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct);
    Task<IReadOnlyList<AppUser>> ListAsync(Role? role, CancellationToken ct);
}

public interface IGuidelineRepository
{
    Task<Guideline?> GetByIdAsync(string id, CancellationToken ct);
    Task<bool> ExistsAsync(string id, CancellationToken ct);
    /// <summary>id → título das orientações que ainda existem (órfãs ficam de fora).</summary>
    Task<IReadOnlyDictionary<string, string>> GetTitlesAsync(IEnumerable<string> ids, CancellationToken ct);
    /// <summary>Ordenada por <c>updatedAt</c> desc.</summary>
    Task<IReadOnlyList<Guideline>> ListAsync(CancellationToken ct);
    Task AddAsync(Guideline guideline, CancellationToken ct);
    void Remove(Guideline guideline);
}

public sealed record GuidelineHistoryQuery(
    string? GuidelineId = null, Pillar? Category = null, string? Campaign = null, DateTime? From = null, DateTime? To = null);

public interface IGuidelineHistoryRepository
{
    Task AddAsync(GuidelineHistoryEntry entry, CancellationToken ct);
    /// <summary>Mais recente primeiro.</summary>
    Task<PagedResult<GuidelineHistoryEntry>> QueryAsync(GuidelineHistoryQuery query, PageRequest page, CancellationToken ct);
}

public enum IdeaSort { CREATED_DESC, ICE_SCORE_DESC }

public sealed record IdeaQuery(
    string? AuthorId = null,
    IReadOnlyCollection<IdeaStatus>? Statuses = null,
    string? GuidelineId = null,
    Division? Division = null,
    IdeaSort Sort = IdeaSort.CREATED_DESC);

public interface IIdeaRepository
{
    Task<Idea?> GetByIdAsync(string id, CancellationToken ct);
    Task AddAsync(Idea idea, CancellationToken ct);
    void Remove(Idea idea);
    Task<PagedResult<Idea>> QueryAsync(IdeaQuery query, PageRequest page, CancellationToken ct);
    /// <summary>Todas as ideias do autor (avaliação de badges).</summary>
    Task<IReadOnlyList<Idea>> ListByAuthorAsync(string authorId, CancellationToken ct);
    /// <summary>Todas as ideias (relatórios), opcionalmente de uma divisão.</summary>
    Task<IReadOnlyList<Idea>> ListAllAsync(Division? division, CancellationToken ct);
}

public sealed record ProjectQuery(ProjectStage? Stage = null, Division? Division = null, string? GuidelineId = null);

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string id, CancellationToken ct);
    Task<Project?> GetByOriginatingIdeaIdAsync(string ideaId, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetByOriginatingIdeaIdsAsync(IEnumerable<string> ideaIds, CancellationToken ct);
    Task AddAsync(Project project, CancellationToken ct);
    void Remove(Project project);
    /// <summary>Ordenada por <c>updatedAt</c> desc.</summary>
    Task<PagedResult<Project>> QueryAsync(ProjectQuery query, PageRequest page, CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAllAsync(Division? division, CancellationToken ct);
}

public interface IProjectUpdateRepository
{
    Task AddAsync(ProjectUpdate update, CancellationToken ct);
    /// <summary>Mais recente primeiro.</summary>
    Task<PagedResult<ProjectUpdate>> ListByProjectAsync(string projectId, PageRequest page, CancellationToken ct);
    Task RemoveByProjectAsync(string projectId, CancellationToken ct);
}

public interface IPointEventRepository
{
    Task AddAsync(PointEvent pointEvent, CancellationToken ct);
    /// <summary>Eventos com <c>createdAt</c> em [from, to).</summary>
    Task<IReadOnlyList<PointEvent>> ListBetweenAsync(DateTime fromInclusive, DateTime toExclusive, CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<RefreshToken>> ListByFamilyAsync(string familyId, CancellationToken ct);
}

public sealed record CachedInsight(
    string CacheKey, string UserId, string Filters, string Model, string PayloadJson, DateTime CreatedAt, DateTime ExpiresAt);

public interface IInsightCache
{
    /// <summary>Entrada ainda válida (não expirada) para a chave, ou <c>null</c>.</summary>
    Task<CachedInsight?> GetAsync(string cacheKey, DateTime now, CancellationToken ct);
    Task SetAsync(CachedInsight insight, CancellationToken ct);
    /// <summary>Quantas gerações foram gravadas desde <paramref name="since"/> (teto diário da cota).</summary>
    Task<int> CountCreatedSinceAsync(DateTime since, CancellationToken ct);
}
