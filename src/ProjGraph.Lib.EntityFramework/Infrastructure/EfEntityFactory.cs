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

    /// <summary>
    /// Replaces the <see cref="EfModel.Entities"/> slot whose <see cref="EfEntity.EffectiveKey"/> matches
    /// <paramref name="updated"/>'s, if present. The slot is found by EffectiveKey, not by reference:
    /// reference equality would silently stop updating the model list the moment the walkers' dictionary
    /// and the model list hold different object instances for the same logical entity (these are init-only
    /// records replaced wholesale, so nothing guarantees they stay the same instance forever) — and not by
    /// <see cref="EfEntity.Name"/>, which is the CLR type name and is not unique across owned entities.
    /// </summary>
    /// <param name="model">The model whose entity list to update.</param>
    /// <param name="updated">The replacement entity.</param>
    public static void ReplaceModelSlot(EfModel model, EfEntity updated)
    {
        for (var i = 0; i < model.Entities.Count; i++)
        {
            if (model.Entities[i].EffectiveKey == updated.EffectiveKey)
            {
                model.Entities[i] = updated;
                return;
            }
        }
    }
}
