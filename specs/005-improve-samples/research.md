# Research Report: Improving .NET Sample Projects structure

**Date**: 2026-02-17
**Topic**: Best practices for organizing samples, .slnx adoption, Mermaid snapshot management, and E-commerce schema design.

---

## 1. Best Practices for Organizing .NET Sample Projects

Research into high-quality .NET repositories (e.g., [dotnet/samples](https://github.com/dotnet/samples)) reveals several industry-standard practices:

### Structural Organization

- **Categorization**: Group samples by feature or technology (e.g., `classdiagram/`, `erd/`, `visualize/`).
- **Isolated Samples**: Each sample should be self-contained in its own directory, allowing users to copy-paste the entire folder into their own environment.
- **Top-Level `Directory.Build.props`**: Use a shared `Directory.Build.props` in the `samples/` root to enforce consistent .NET versions, nullable settings, and common dependencies (like `xUnit` for tests) across all samples.
- **Cleanliness**: Ensure `bin/` and `obj/` are globally ignored. Samples should be buildable immediately after cloning without manual cleanup.

### Documentation Standards

- **Standardized READMEs**: Every sample folder should contain a `README.md` with:
  - **Description**: What does this sample demonstrate?
  - **Quick Start**: The exact `dotnet` and `projgraph` commands to run.
  - **Visual Reference**: A link or embedded Mermaid diagram showing the expected output.
  - **Professional Naming**: Avoid generic names like `TestClass`, `Foo`, or `Bar`. Use domain-specific terms (e.g., `OrderProcessor`, `CustomerRepository`).

---

## 2. Evaluation: .slnx vs .sln for .NET 10

With the advent of .NET 10, the solution management landscape is shifting towards the `.slnx` format.

| Feature | Legacy `.sln`                         | New `.slnx` |
|---------|---------------------------------------|-------------|
| **Format** | Proprietary Text-based (Hard to read) | XML-based (Human-readable) |
| **Maintenance** | Prone to merge conflicts              | Clean, predictable XML structure |
| **Tooling Support** | Universal                             | Supported in VS 2022+ and .NET CLI 9.0.200+ |
| **Default in .NET 10** | No                                    | **Yes** (`dotnet new sln` default) |

### Recommendation

For the `ProjGraph` samples, **adopt `.slnx`** where multi-project solutions are needed. It aligns with modern .NET defaults and is significantly easier to maintain within a git-based workflow. For single-project samples, a `.csproj` is sufficient and avoids "solution clutter."

---

## 3. Recommended Visual Snapshot Workflow

To provide Mermaid snapshots that are readable on GitHub without cluttering the project structure:

- **Directory Pattern**: Create a `snapshots/` folder within each sample directory.
- **Naming Convention**: Use descriptive names like `class-hierarchy.mmd` or `entity-relationships.mmd`.
- **GitHub Integration**:
  - embed the Mermaid code blocks directly in the sample's `README.md` using:

  ```mermaid
  [Mermaid Code Here]
  ```

  - GitHub renders these natively, providing immediate visual feedback.
  - **Automation**: Provide a `scripts/regenerate-samples.ps1` that uses the ProjGraph CLI to overwrite these `.mmd` files and README sections, ensuring documentation never drifts from the code.

---

## 4. Complex E-commerce ERD Schema (10+ Entities)

Designed for the `erd/complex-ecommerce` sample to showcase ProjGraph's ability to handle deep hierarchies and many-to-many relationships.

### Entities & Relationships

1. **Customer**: Core user data.
2. **Address**: One-to-Many with *Customer* (Billing, Shipping).
3. **Product**: One-to-Many with *Category*.
4. **Category**: Recursive relationship (*ParentCategory*) for deep hierarchies.
5. **Order**: One-to-Many with *Customer*.
6. **OrderItem**: Many-to-One with *Order* and *Product*.
7. **Payment**: One-to-One with *Order*.
8. **Review**: Many-to-One with *Customer* and *Product*.
9. **ShoppingCart**: One-to-One with *Customer*.
    **ShoppingCartItem**: Many-to-One with *ShoppingCart* and *Product*.
10. **Supplier**: Core vendor data.
11. **ProductSupplier**: Many-to-Many join table between *Product* and *Supplier*.

### Key Features to Demonstrate

- **Enums**: Payment methods (*CreditCard, PayPal, WireTransfer*).
- **Inheritance**: Perhaps separate *PhysicalProduct* and *DigitalProduct* inheriting from *Product*.
- **Temporal Data**: `CreatedAt`, `UpdatedAt` fields across most entities to show metadata handling.
- **Complex FKs**: Recursive FK in `Category` and Many-to-Many via a join entity (`ProductSupplier`).
