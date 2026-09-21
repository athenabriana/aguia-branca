using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Ideas;

internal static class IdeaAccess
{
    /// <summary>Operador só enxerga as próprias ideias; gestor e líder enxergam todas (R2-03.3).</summary>
    public static bool CanSee(this ICurrentUser user, Idea idea) => user.Role != Role.OPERADOR || idea.AuthorId == user.Id;

    /// <summary>Somente o autor altera/exclui. Quem não enxerga a ideia recebe 404; quem enxerga mas não é o autor, 403.</summary>
    public static Error? DenyEdit(this ICurrentUser user, Idea idea) =>
        idea.AuthorId == user.Id ? null : user.CanSee(idea) ? IdeaErrors.Forbidden : IdeaErrors.NotFound;
}
