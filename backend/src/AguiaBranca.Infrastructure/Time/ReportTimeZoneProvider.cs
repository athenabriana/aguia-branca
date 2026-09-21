using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Time;

internal sealed class ReportTimeZoneProvider(IOptions<ReportsOptions> options) : ITimeZoneProvider
{
    // Já validado no startup (ReportsOptionsValidator).
    public TimeZoneInfo ReportTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
}
