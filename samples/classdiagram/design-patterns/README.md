# Design Patterns - Complex Class Diagram Example

A comprehensive C# sample project demonstrating advanced OOP concepts, design patterns, and complex class relationships
for the **Class Diagram** feature.

## Model Structure

This example showcases:

### Architectural Patterns

- **Repository Pattern**: Generic repository with unit of work
- **Factory Pattern**: Object creation abstraction
- **Strategy Pattern**: Interchangeable algorithms
- **Observer Pattern**: Event notification system
- **Decorator Pattern**: Dynamic behavior extension

### Advanced OOP Concepts

- **Multiple inheritance levels** (3+ levels deep)
- **Generic constraints** (`where T : class`)
- **Interface segregation** (multiple interfaces)
- **Composition over inheritance**
- **Abstract base classes**
- **Static factory methods**
- **Fluent builder pattern**

### Complex Relationships

- **Inheritance hierarchies**: Entity → AuditableEntity → User/Product/Order
- **Composition**: Order contains OrderItems, PaymentInfo, ShippingAddress
- **Aggregation**: ShoppingCart aggregates Products
- **Dependencies**: Services depend on repositories
- **Associations**: Many-to-many through junction tables

## Project Structure

```
Base/
├── Entity.cs                    - Base entity with Id
├── AuditableEntity.cs          - Adds audit fields (CreatedBy, UpdatedAt, etc.)
└── IEntity.cs                   - Entity marker interface

Domain/
├── User.cs                      - User entity with roles and orders
├── Product.cs                   - Product with categories and inventory
├── Order.cs                     - Order with items and payment
├── OrderItem.cs                 - Order line item
├── Category.cs                  - Product categorization
├── ShoppingCart.cs              - Shopping cart aggregate
└── Payment.cs                   - Payment information

Enums/
├── OrderStatus.cs               - Order state enum
├── PaymentMethod.cs             - Payment type enum
└── UserRole.cs                  - User role enum

Interfaces/
├── IRepository.cs               - Generic repository interface
├── IUnitOfWork.cs               - Unit of work pattern
├── INotificationService.cs      - Observer pattern interface
├── IPaymentStrategy.cs          - Strategy pattern interface
├── IPricingStrategy.cs          - Pricing calculation strategy
└── IValidator.cs                - Validation interface

Services/
├── UserService.cs               - User business logic
├── OrderService.cs              - Order processing
├── NotificationService.cs       - Event notifications
├── PaymentProcessor.cs          - Payment handling
└── PricingService.cs            - Price calculations

Repositories/
├── Repository.cs                - Generic repository implementation
├── UserRepository.cs            - User-specific queries
├── OrderRepository.cs           - Order-specific queries
└── UnitOfWork.cs                - Transaction management

Builders/
└── OrderBuilder.cs              - Fluent order builder

Strategies/
├── CreditCardPayment.cs         - Credit card payment strategy
├── PayPalPayment.cs             - PayPal payment strategy
├── StandardPricing.cs           - Standard pricing strategy
└── DiscountPricing.cs           - Discount pricing strategy

Validators/
├── UserValidator.cs             - User validation rules
└── OrderValidator.cs            - Order validation rules
```

## Usage Examples

### Analyze Complete Domain

```bash
# Start from the main aggregate root
projgraph classdiagram Domain/Order.cs

# Analyze the entire service layer
projgraph classdiagram Services/OrderService.cs

# View the repository pattern implementation
projgraph classdiagram Repositories/Repository.cs
```

### Specific Patterns

```bash
# Strategy pattern visualization
projgraph classdiagram Interfaces/IPaymentStrategy.cs

# Builder pattern
projgraph classdiagram Builders/OrderBuilder.cs

# Repository pattern with generic constraints
projgraph classdiagram Repositories/UserRepository.cs
```

### Discovery Options

```bash
# Deep discovery (show all relationships up to depth 5)
projgraph classdiagram Domain/Order.cs --depth 5

# Include only direct dependencies
projgraph classdiagram Services/OrderService.cs --depth 1
```

## Key Features Demonstrated

### 1. **Complex Inheritance Chains**

```
IEntity → Entity → AuditableEntity → User/Product/Order
```

### 2. **Generic Constraints**

```csharp
IRepository<T> where T : class, IEntity
Repository<T> : IRepository<T> where T : Entity
```

### 3. **Multiple Interfaces**

```csharp
OrderService : IOrderService, INotificationService
```

### 4. **Composition & Aggregation**

- Order **composes** OrderItems (strong ownership)
- ShoppingCart **aggregates** Products (weak reference)

### 5. **Strategy Pattern**

- Interchangeable payment methods
- Different pricing strategies

### 6. **Builder Pattern**

- Fluent API for complex object creation

## Expected Output

The tool will generate a comprehensive Mermaid diagram showing:

- ✅ Multiple inheritance levels
- ✅ Generic type parameters with constraints
- ✅ Interface implementations
- ✅ Composition relationships (filled diamonds)
- ✅ Association relationships (lines)
- ✅ Dependency relationships (dashed lines)
- ✅ Cardinality markers (1, *, 0..1)
- ✅ Abstract classes marked with `<<abstract>>`
- ✅ Interface markers with `<<interface>>`
- ✅ Enum types with `<<enumeration>>`

## Running the Sample

1. Navigate to the sample directory:
   ```bash
   cd samples\classdiagram\design-patterns
   ```

2. Run the class diagram tool:
   ```bash
   projgraph classdiagram Domain/Order.cs
   ```

3. The output will be a complete Mermaid class diagram showing the entire domain model with all relationships.

## Learning Outcomes

This sample demonstrates:

- ✅ How to model complex business domains
- ✅ Proper application of design patterns
- ✅ Clean architecture principles
- ✅ SOLID principles in action
- ✅ Advanced C# features (generics, constraints, etc.)
- ✅ How the class diagram tool handles real-world complexity
