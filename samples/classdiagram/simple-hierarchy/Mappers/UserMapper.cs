using SimpleHierarchy.Dtos;
using SimpleHierarchy.Models;

namespace SimpleHierarchy.Mappers;

public class UserMapper<TEntity, TDto> where TEntity : User, new() where TDto : UserDto
{
    public TDto ToDto(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return (TDto)Activator.CreateInstance(typeof(TDto), entity.Username, entity.Email)!;
    }

    public TEntity ToEntity(TDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = new TEntity { Username = dto.Username, Email = dto.Email };

        return entity;
    }

    public void UpdateEntity(TEntity entity, TDto dto)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.Username = dto.Username;
        entity.Email = dto.Email;
    }
}
