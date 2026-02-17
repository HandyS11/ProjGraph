# CLI Command Contracts: ProjGraph Samples

This document defines the exact CLI command patterns used to generate the reference diagrams for each sample.

## 1. ERD: Complex E-commerce

Generates an entity-relationship diagram for the complex database model.

**Command:**

```bash
projgraph erd ./samples/erd/complex-ecommerce/MyDbContext.cs > ./samples/erd/complex-ecommerce/snapshots/erd.mmd
```

**Key Parameters:**

- `[path]`: The first argument is the path to the DbContext source file.
- `> shell-redirection`: Used to save the Mermaid output to a snapshot file.

---

## 2. Class Diagram: Design Patterns

Generates a detailed class diagram showing inheritance, interfaces, and associations.

**Command:**

```bash
projgraph classdiagram ./samples/classdiagram/design-patterns/Domain/Order.cs --inheritance --dependencies --depth 2 --properties true --functions true > ./samples/classdiagram/design-patterns/snapshots/design-patterns.mmd
```

**Key Parameters:**

- `[path]`: Path to the .cs file to analyze.
- `--inheritance`: Auto-discover base classes in the workspace.
- `--dependencies`: Auto-discover related types (properties/fields).
- `--depth 2`: Traverse relationships up to 2 levels deep.
- `--properties true`: Show properties/fields in the diagram.
- `--functions true`: Show functions/methods in the diagram.

---

## 3. Project Graph: Modular Architecture

Generates a high-level project dependency graph for the entire workspace.

**Command:**

```bash
projgraph visualize ./samples/visualize/modular-architecture/ModularArchitecture.slnx --format mermaid > ./samples/visualize/modular-architecture/snapshots/dependencies.mmd
```

**Key Parameters:**

- `[path]`: Path to the .slnx solution file.
- `--format mermaid`: Ensure output is in Mermaid format.

---

## 4. Class Diagram: Simple Hierarchy

A basic entry-level sample for new users.

**Command:**

```bash
projgraph classdiagram ./samples/classdiagram/simple-hierarchy/Models/User.cs --properties false --functions false > ./samples/classdiagram/simple-hierarchy/snapshots/simple-hierarchy.md
```

**Key Parameters:**

- `--properties false`: Hide properties/fields for a cleaner view.
- `--functions false`: Hide functions/methods.
