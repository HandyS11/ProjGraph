# Data Model Design: Showcase Samples

This document defines the domain models and project structures for the ProjGraph showcase samples.

## 1. ERD: Complex E-commerce (`erd/complex-ecommerce/`)

This sample demonstrates advanced Entity Framework Core mapping, many-to-many relationships, and recursive hierarchies.

### Entities

| Entity | Description                                        | Key Relationships |
|--------|----------------------------------------------------|-------------------|
| **Customer** | Professional profile with contact info             | 1:N Addresses, 1:N Orders, 1:1 ShoppingCart |
| **Address** | Multiple addresses per customer                    | N:1 Customer |
| **Product** | Base product with inventory and pricing            | N:1 Category, 1:N OrderItems, N:N Suppliers |
| **DigitalProduct** | Inherits from Product; adds `DownloadUrl`          | inherits Product |
| **PhysicalProduct** | Inherits from Product; adds `Weight`, `Dimensions` | inherits Product |
| **Category** | Hierarchical categorization of products            | 1:N Products, N:1 ParentCategory (Recursive) |
| **Order** | Core sales entity                                  | N:1 Customer, 1:N OrderItems, 1:1 Payment |
| **OrderItem** | Line items for an order                            | N:1 Order, N:1 Product |
| **Payment** | Financial transaction details                      | 1:1 Order |
| **Review** | User-generated feedback                            | N:1 Customer, N:1 Product |
| **ShoppingCart** | Volatile user cart                                 | 1:1 Customer, 1:N ShoppingCartItems |
| **ShoppingCartItem** | Items currently in a cart                          | N:1 ShoppingCart, N:1 Product |
| **Supplier** | Inventory source                                   | N:N Products (via ProductSupplier) |
| **ProductSupplier** | Join entity for many-to-many relationship          | N:1 Product, N:1 Supplier |

### EF Core Features Demonstrated

- **TPH (Table-Per-Hierarchy)**: `DigitalProduct` and `PhysicalProduct` inheriting from `Product`.
- **Recursive FK**: `Category` → `ParentCategory`.
- **Shadow Properties**: `CreatedAt`, `LastModifiedAt` for all entities.
- **Owned Types**: `Money` (Amount, Currency) used in `Product` and `Payment`.

---

## 2. Class Diagram: Design Patterns (`classdiagram/design-patterns/`)

This sample showcases advanced C# features, abstractions, and standard software engineering patterns.

### Pattern Implementations

#### Creational Patterns

- **Generic Repository**: `IRepository<T> where T : class, IEntity`
- **Unit of Work**: `IUnitOfWork` orchestrating multiple repositories.
- **Fluent Builder**: `OrderBuilder` for complex `Order` construction.

#### Structural Patterns

- **Decorator**: `LoggingNotificationService` decorating `EmailNotificationService`.
- **Composite**: `CompositeValidator` aggregating multiple `IValidator<T>` instances.

#### Behavioral Patterns

- **Strategy**: `IPricingStrategy` (Standard, Discounted, Seasonal).
- **Observer**: `IDomainEvent` and `IEventHandler`.
- **Command**: `ICommand` (ProcessOrder, CancelOrder).

### Advanced C# Features

- **File-scoped namespaces**.
- **Records**: Used for DTOs and value objects.
- **Primary Constructors**: Used in Service implementations.
- **Generic Constraints**: Multi-level generic inheritance.

---

## 3. Project Graph: Modular Workspace (`visualize/modular-architecture/`)

This sample demonstrates ProjGraph's ability to map cross-project dependencies.

### Structure

- **ProjGraph.App**: The main entry point (CLI app).
- **ProjGraph.Core**: Domain models and abstractions (referenced by all).
- **ProjGraph.Infrastructure**: Database and external API implementations.
- **ProjGraph.Services**: Orchestration logic.
- **ProjGraph.Shared**: Common utilities and extensions.

### Dependency Flow

- `App` -> `Services`, `Infrastructure`, `Core`
- `Services` -> `Infrastructure`, `Core`
- `Infrastructure` -> `Core`
- `Shared` is a standalone leaf node.
