using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Domain.Entities;

/// <summary>
/// Usuário da plataforma. Também é o "usuário Identity" (via store customizado): os campos de credencial
/// (hash, security stamp, lockout) são dados de persistência gerenciados pelo UserManager, por isso têm setter público.
/// </summary>
public sealed class AppUser
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Role Role { get; private set; }
    public Division Division { get; private set; }
    public int Points { get; private set; }
    public List<string> Badges { get; private set; } = [];
    public string? LegacyId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Token de concorrência otimista: incrementa a cada alteração persistida. Evita que uma escrita "de documento inteiro"
    /// (ex.: contador de falhas de login pelo Identity) sobrescreva pontos/badges alterados por outra transação.
    /// </summary>
    public int Version { get; private set; }

    // --- credenciais (Identity) ---
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }

    private AppUser() { }

    public static AppUser Create(string name, string email, Role role, Division division, DateTime now, string? legacyId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw DomainException.Validation(nameof(name), "Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(email)) throw DomainException.Validation(nameof(email), "E-mail é obrigatório.");

        return new AppUser
        {
            Id = EntityId.New(),
            Name = name.Trim(),
            Email = email.Trim(),
            UserName = email.Trim(),
            Role = role,
            Division = division,
            LegacyId = legacyId,
            CreatedAt = now,
            Version = 1
        };
    }

    /// <summary>Marca o documento como alterado (bump de <see cref="Version"/>). Chamado pelo store do Identity a cada gravação.</summary>
    public void MarkModified() => Version++;

    /// <summary>Aplica o delta com clamp em 0 (R-06.7) e devolve o delta <b>efetivo</b>.</summary>
    public int ApplyPoints(int delta)
    {
        var next = Math.Max(0, Points + delta);
        var effective = next - Points;
        Points = next;
        if (effective != 0) Version++;
        return effective;
    }

    /// <summary>Adiciona badges ainda não possuídas; devolve somente as novas.</summary>
    public IReadOnlyList<string> AddBadges(IEnumerable<string> badges)
    {
        var added = new List<string>();
        foreach (var badge in badges)
        {
            if (Badges.Contains(badge)) continue;
            Badges.Add(badge);
            added.Add(badge);
        }
        if (added.Count > 0) Version++;
        return added;
    }
}
