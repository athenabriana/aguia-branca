using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Ai;

namespace AguiaBranca.Infrastructure.Tests.Ai;

public class GeminiInsightParserTests
{
    [Fact]
    public void Priority_IsCaseInsensitive_AndBlankRelatedRefBecomesNull()
    {
        var insight = GeminiInsightParser.Parse(
            """{"summary":"s","highlights":[],"risks":[],"recommendations":[{"title":"t","detail":"d","priority":"media","relatedGuidelineRef":"  "}]}""")!;

        insight.Recommendations.Single().Priority.Should().Be(InsightPriority.MEDIA);
        insight.Recommendations.Single().RelatedGuidelineRef.Should().BeNull();
    }

    [Fact]
    public void LongTextsAreTruncated_AndListsAreCapped_AndBlankBulletsDropped()
    {
        var many = string.Join(",", Enumerable.Range(0, 20).Select(i => $"\"item {i}\""));
        var insight = GeminiInsightParser.Parse(
            $$"""{"summary":"{{new string('x', 3000)}}","highlights":[{{many}},"  "],"risks":["", "só este"],"recommendations":[]}""")!;

        insight.Summary.Length.Should().Be(GeminiInsightParser.SummaryMax);
        insight.Summary.Should().EndWith("…");
        insight.Highlights.Should().HaveCount(GeminiInsightParser.MaxItems);
        insight.Risks.Should().Equal("só este");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"summary":"s","highlights":[],"risks":[],"recommendations":[null]}""")]
    [InlineData("""{"summary":123,"highlights":[],"risks":[],"recommendations":[]}""")]
    public void Malformed_ReturnsNull(string json) => GeminiInsightParser.Parse(json).Should().BeNull();
}
