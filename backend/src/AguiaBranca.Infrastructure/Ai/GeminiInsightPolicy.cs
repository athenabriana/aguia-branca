using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Ai;

internal sealed class GeminiInsightPolicy(IOptions<GeminiOptions> options) : IInsightPolicy
{
    public TimeSpan CacheDuration => TimeSpan.FromHours(options.Value.CacheHours);
    public int DailyLimit => options.Value.DailyLimit;
}
