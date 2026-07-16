using ProjGraph.Core.Models;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Factory for <see cref="EfEntity"/> instances. Init-only setters make in-place mutation impossible,
/// so walkers replace entities wholesale; centralising the copy here keeps every field — notably the
/// owned-type metadata — from being silently dropped when a single field is rewritten.
/// </summary>
internal static class EfEntityFactory
{
    /// <summary>
    /// Creates a copy of <paramref name="source"/>, optionally overriding the table name.
    /// </summary>
    /// <param name="source">The entity to copy.</param>
    /// <param name="tableName">The replacement table name, or <see langword="null"/> to keep the source's.</param>
    public static EfEntity CopyWith(EfEntity source, string? tableName = null)
    {
        var copy = new EfEntity
        {
            Name = source.Name,
            IsJoinEntity = source.IsJoinEntity,
            TableName = tableName ?? source.TableName,
            Key = source.Key,
            IsOwned = source.IsOwned,
            OwnerEntity = source.OwnerEntity,
            NavigationName = source.NavigationName,
            IsCollection = source.IsCollection
        };

        foreach (var property in source.Properties)
        {
            copy.Properties.Add(property);
        }

        return copy;
    }
}
