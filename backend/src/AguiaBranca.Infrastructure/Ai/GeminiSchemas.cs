using System.Text.Json.Nodes;

namespace AguiaBranca.Infrastructure.Ai;

/// <summary>Schema de saída estruturada enviado ao Gemini (<c>generationConfig.responseSchema</c>, subconjunto OpenAPI).</summary>
internal static class GeminiSchemas
{
    public static readonly string[] Priorities = ["ALTA", "MEDIA", "BAIXA"];

    /// <summary>Nova instância a cada chamada (um <see cref="JsonNode"/> só pode ter um pai).</summary>
    public static JsonObject Insight() => new()
    {
        ["type"] = "OBJECT",
        ["properties"] = new JsonObject
        {
            ["summary"] = new JsonObject { ["type"] = "STRING" },
            ["highlights"] = StringArray(),
            ["risks"] = StringArray(),
            ["recommendations"] = new JsonObject
            {
                ["type"] = "ARRAY",
                ["items"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new JsonObject
                    {
                        ["title"] = new JsonObject { ["type"] = "STRING" },
                        ["detail"] = new JsonObject { ["type"] = "STRING" },
                        ["priority"] = new JsonObject
                        {
                            ["type"] = "STRING",
                            ["enum"] = new JsonArray(Priorities.Select(p => (JsonNode)p).ToArray())
                        },
                        ["relatedGuidelineRef"] = new JsonObject { ["type"] = "STRING", ["nullable"] = true }
                    },
                    ["required"] = new JsonArray("title", "detail", "priority")
                }
            }
        },
        ["required"] = new JsonArray("summary", "highlights", "risks", "recommendations")
    };

    private static JsonObject StringArray() =>
        new() { ["type"] = "ARRAY", ["items"] = new JsonObject { ["type"] = "STRING" } };
}
