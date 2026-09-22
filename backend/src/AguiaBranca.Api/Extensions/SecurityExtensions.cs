using System.Text;
using System.Threading.RateLimiting;
using AguiaBranca.Api.Authorization;
using AguiaBranca.Api.Http;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using AspCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AguiaBranca.Api.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddAguiaBrancaSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Parâmetros de validação vêm das options já validadas no startup (chave >= 32 bytes etc.).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false; // mantém os nomes do token: sub, name, role, division
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorizationBuilder()
            // Toda rota exige autenticação, a menos que marcada com [AllowAnonymous] (login, refresh, health).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.GestorOnly, p => p.RequireRole(nameof(Role.GESTOR)))
            .AddPolicy(Policies.LiderOnly, p => p.RequireRole(nameof(Role.LIDER)))
            .AddPolicy(Policies.CanCreateIdea, p => p.RequireRole(nameof(Role.OPERADOR), nameof(Role.GESTOR)))
            .AddPolicy(Policies.ProjectsRead, p => p.RequireRole(nameof(Role.GESTOR), nameof(Role.LIDER)))
            .AddPolicy(Policies.UsersRead, p => p.RequireRole(nameof(Role.GESTOR), nameof(Role.LIDER)));

        AddHardening(services);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // /auth/*: por IP (login, refresh compartilham o balde) — força bruta de credenciais.
            limiter.AddPolicy(RateLimitPolicies.Auth, context =>
            {
                var o = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = o.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(o.AuthWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            // Insights de IA: por usuário (a cota do Gemini é compartilhada e limitada).
            limiter.AddPolicy(RateLimitPolicies.Insights, context =>
            {
                var o = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var key = context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = o.InsightsPermitLimit,
                        Window = TimeSpan.FromSeconds(o.InsightsWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            limiter.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                await ApiProblems.WriteAsync(http, ApiProblems.Create(
                    http, StatusCodes.Status429TooManyRequests, "RATE_LIMITED", "Muitas requisições. Aguarde e tente novamente."));
            };
        });

        return services;
    }

    /// <summary>CORS restrito às origens configuradas, HSTS e (opcional) headers de proxy reverso.</summary>
    private static void AddHardening(IServiceCollection services)
    {
        services.AddCors();
        // Só as origens de <c>Cors:Origins</c>; sem nenhuma, nenhuma origem de navegador é liberada (o app nativo não usa CORS).
        services.AddOptions<AspCorsOptions>().Configure<IOptions<Infrastructure.Configuration.CorsOptions>>((cors, mine) =>
            cors.AddDefaultPolicy(policy =>
            {
                if (mine.Value.Origins.Length == 0) return;
                policy.WithOrigins(mine.Value.Origins)
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID")
                    .WithExposedHeaders("X-Correlation-ID", "Retry-After")
                    .SetPreflightMaxAge(TimeSpan.FromHours(1));
            }));

        services.AddOptions<HstsOptions>().Configure<IOptions<SecurityOptions>>((hsts, security) =>
        {
            hsts.MaxAge = TimeSpan.FromDays(security.Value.HstsMaxAgeDays);
            hsts.IncludeSubDomains = true;
        });

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // O proxy do host (Render/Azure/Nginx) não tem IP fixo conhecido: a confiança é ligada por configuração.
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });
    }
}
