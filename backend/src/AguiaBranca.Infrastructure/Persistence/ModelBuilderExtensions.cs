using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Extensions;

namespace AguiaBranca.Infrastructure.Persistence;

internal static class ModelBuilderExtensions
{
    /// <summary>Id/referência como <c>string</c> no Domain e <c>ObjectId</c> nativo no Mongo (validado no spike B03).</summary>
    public static PropertyBuilder<string> AsObjectId(this PropertyBuilder<string> builder) =>
        builder.HasConversion(v => ObjectId.Parse(v), v => v.ToString());

    public static PropertyBuilder<string?> AsNullableObjectId(this PropertyBuilder<string?> builder) =>
        builder.HasConversion(
            v => v == null ? (ObjectId?)null : ObjectId.Parse(v),
            v => v == null ? null : v.ToString());

    public static PropertyBuilder<string> AsDocumentId(this PropertyBuilder<string> builder) =>
        builder.AsObjectId().HasElementName("_id");

    /// <summary>
    /// Aplica camelCase aos nomes de campo (o provider usa PascalCase por padrão). <c>OwnsOne/OwnsMany</c>
    /// precisam de <c>HasElementName</c> explícito nas configurações (não há API de metadados para navegações).
    /// </summary>
    public static void UseCamelCaseElementNames(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entityType.GetProperties())
        {
            if (property.IsPrimaryKey()) continue;
            property.SetElementName(char.ToLowerInvariant(property.Name[0]) + property.Name[1..]);
        }
    }
}
