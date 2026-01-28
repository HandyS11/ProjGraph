# Complex Class Hierarchy Example

A C# sample project demonstrating complex inheritance, generic types, interfaces, and cross-file dependencies for the *
*Class Diagram** feature.

## Structure

This example includes:

### Domain

- **IEntity**: Base interface for all entities.
- **AuditableEntity**: Abstract base class implementing `IEntity` with audit properties.
- **Person**: Abstract base class inheriting from `AuditableEntity`.
- **User**: Inherits from `Person`.
- **Employee**: Inherits from `Person`.
- **Address**: A record used as a property in `Person` (Association).
- **AccessLevel**: An enum used in `User`.

### Repositories

- **IRepository<T>**: A generic interface for repository operations.
- **IUserRepository**: A specialized interface inheriting from `IRepository<User>`.

### Services

- **IService<T>**: A generic interface for service operations.
- **ServiceBase<T>**: An abstract base class implementing `IService<T>` and depending on `IRepository<T>`.
- **UserService**: Inherits from `ServiceBase<User>` and depends on `IUserRepository`.

## Purpose

This sample is designed to test and demonstrate:

1. **Inheritance**: Multiple levels of inheritance (`User` -> `Person` -> `AuditableEntity`).
2. **Realization**: Classes implementing interfaces (`AuditableEntity` : `IEntity`, `ServiceBase<T>` : `IService<T>`).
3. **Generics**: Proper rendering of generic types like `IService~T~` in Mermaid.
4. **Associations**: Identifying dependencies between classes (e.g., `Person` -> `Address`).
5. **Enums**: Grouping and displaying enum types.

## Usage

You can use the `get_class_diagram` tool on files in this project to see how it handles these relationships.

Example:
Run on `UserService.cs` with `includeInheritance` and `includeDependencies` to see the full service/repository/domain
interaction.
