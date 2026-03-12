# ProjGraph.Core

Core domain models, exceptions, and shared abstractions for the ProjGraph ecosystem.

[![NuGet](https://img.shields.io/nuget/v/ProjGraph.Core)](https://www.nuget.org/packages/ProjGraph.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation

```bash
dotnet add package ProjGraph.Core
```

## Overview

`ProjGraph.Core` is the foundational package for the ProjGraph ecosystem. It provides the shared domain models,
enumerations, and exception types consumed by all higher-level ProjGraph libraries.

This package is typically not referenced directly — prefer one of the higher-level packages:

- [`ProjGraph.Lib.ProjectGraph`](https://www.nuget.org/packages/ProjGraph.Lib.ProjectGraph) — project dependency graph
- [`ProjGraph.Lib.ClassDiagram`](https://www.nuget.org/packages/ProjGraph.Lib.ClassDiagram) — class hierarchy analysis
- [`ProjGraph.Lib.EntityFramework`](https://www.nuget.org/packages/ProjGraph.Lib.EntityFramework) — EF Core ERD
  generation
- [`ProjGraph.Lib`](https://www.nuget.org/packages/ProjGraph.Lib) — meta-package (all of the above)

## Models

| Type               | Description                                                       |
|--------------------|-------------------------------------------------------------------|
| `Project`          | Represents a .NET project or package node in the dependency graph |
| `Dependency`       | Represents a directed edge between two `Project` nodes            |
| `DependencyType`   | Enum: `ProjectReference`, `PackageReference`                      |
| `ProjectType`      | Enum: `Project`, `Package`                                        |
| `ClassModel`       | Roslyn-derived class/record/interface descriptor                  |
| `MemberDefinition` | Field, property, or method descriptor within a `ClassModel`       |
| `EfModel`          | Entity Framework database model                                   |
| `EfEntity`         | Table/entity within an EF model                                   |
| `EfProperty`       | Column/property on an `EfEntity`                                  |
| `EfRelationship`   | Foreign-key relationship between two entities                     |

## Exceptions

| Type                 | Description                                                      |
|----------------------|------------------------------------------------------------------|
| `ProjGraphException` | Base exception for all ProjGraph errors                          |
| `ParsingException`   | Thrown when a solution, project, or source file cannot be parsed |
| `AnalysisException`  | Thrown when Roslyn or EF analysis fails                          |

## Links

- [GitHub Repository](https://github.com/HandyS11/ProjGraph)
- [Documentation](https://handys11.github.io/ProjGraph)
