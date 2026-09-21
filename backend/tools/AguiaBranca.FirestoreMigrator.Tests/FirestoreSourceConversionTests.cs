using AguiaBranca.FirestoreMigrator.Source;
using AguiaBranca.FirestoreMigrator.Transform;
using Google.Cloud.Firestore;

namespace AguiaBranca.FirestoreMigrator.Tests;

/// <summary>Conversão dos tipos do SDK do Firestore para os tipos simples que os parsers entendem (sem rede).</summary>
public class FirestoreSourceConversionTests
{
    [Fact]
    public void Timestamp_BecomesAUtcDateTime()
    {
        var ts = Timestamp.FromDateTime(new DateTime(2026, 9, 21, 15, 30, 0, DateTimeKind.Utc));

        var value = FirestoreSource.Convert(ts);

        value.Should().Be(new DateTime(2026, 9, 21, 15, 30, 0, DateTimeKind.Utc));
        ((DateTime)value!).Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void NestedMapsAndLists_AreConvertedRecursively_LikeTheIceMapAndTheUpdateChanges()
    {
        var raw = new Dictionary<string, object>
        {
            ["ice"] = new Dictionary<string, object> { ["impact"] = 9L, ["confidence"] = 8L, ["ease"] = 7L },
            ["changes"] = new List<object> { new Dictionary<string, object> { ["field"] = "targetDate", ["from"] = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), ["to"] = 5L } }
        };

        var converted = (Dictionary<string, object?>)FirestoreSource.Convert(raw)!;
        var fields = new Fields(converted);

        fields.Map("ice")!.Value.Decimal("impact", out _).Should().Be(9m);
        var change = Parsers.Update("p", new SourceDoc("u", new Dictionary<string, object?> { ["authorId"] = "a", ["changes"] = converted["changes"], ["createdAt"] = DateTime.UtcNow }), DateTime.UtcNow, new Migration.IssueLog())!.Changes.Single();
        (change.Field, change.From).Should().Be(("targetDate", "2026-01-01T00:00:00Z"));
    }

    [Fact]
    public void PlainValues_PassThrough()
    {
        FirestoreSource.Convert("texto").Should().Be("texto");
        FirestoreSource.Convert(12L).Should().Be(12L);
        FirestoreSource.Convert(null).Should().BeNull();
    }
}
