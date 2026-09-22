using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Api.Tests.Reports;

/// <summary>Substitui o Gemini nos testes de API: registra o que seria enviado e responde por script.</summary>
public sealed class FakeInsightGenerator : IInsightGenerator
{
    private readonly object _gate = new();
    public string Model => "fake-model-1";
    public bool IsConfigured { get; set; } = true;
    public Func<Result<GeneratedInsight>>? Behavior { get; set; }
    public List<InsightRequest> Requests { get; } = [];
    public int Calls { get { lock (_gate) return Requests.Count; } }

    public Task<Result<GeneratedInsight>> GenerateAsync(InsightRequest request, CancellationToken ct)
    {
        lock (_gate) Requests.Add(request);
        return Task.FromResult(Behavior?.Invoke() ?? Result<GeneratedInsight>.Ok(new GeneratedInsight(
            "Resumo executivo gerado pelo modelo falso.", ["Destaque 1", "Destaque 2"], ["Risco 1"],
            [new GeneratedRecommendation("Ação prioritária", "Detalhe da ação.", InsightPriority.ALTA, "G1"),
             new GeneratedRecommendation("Ação sem orientação", "Detalhe.", InsightPriority.BAIXA, null)])));
    }
}
