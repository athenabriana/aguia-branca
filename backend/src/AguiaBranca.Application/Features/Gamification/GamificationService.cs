using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Application.Features.Gamification;

/// <summary>
/// Pontos e badges no servidor (R2-05). Deve ser usado <b>dentro</b> de <c>IUnitOfWork.ExecuteInTransactionAsync</c>:
/// a razão de pontos, o saldo do usuário e a regra que originou o evento são confirmados juntos.
/// </summary>
public sealed class GamificationService(
    IPointEventRepository events,
    IIdeaRepository ideas,
    IUnitOfWork unitOfWork,
    IClock clock,
    ITimeZoneProvider timeZone)
{
    /// <summary>
    /// Aplica o delta ao usuário (clamp em 0) e grava o evento com o delta <b>efetivo</b>. Devolve o delta efetivo
    /// (pode ser menor, em módulo, que o pedido); delta efetivo 0 não gera evento.
    /// </summary>
    public async Task<int> AwardAsync(AppUser user, PointReason reason, int delta, string? refId, CancellationToken ct)
    {
        var effective = user.ApplyPoints(delta);
        if (effective != 0)
            await events.AddAsync(PointEvent.Create(user.Id, effective, reason, refId, clock.UtcNow), ct);
        return effective;
    }

    /// <summary>
    /// Confirma as alterações pendentes (para que a avaliação enxergue o estado atual das ideias) e concede as badges
    /// ainda não possuídas (R2-05.3). Devolve só as novas.
    /// </summary>
    public async Task<IReadOnlyList<string>> GrantEarnedBadgesAsync(AppUser user, CancellationToken ct)
    {
        await unitOfWork.SaveChangesAsync(ct);
        var authored = await ideas.ListByAuthorAsync(user.Id, ct);
        return user.AddBadges(BadgeEvaluator.Evaluate(user, authored, timeZone.ReportTimeZone));
    }
}
