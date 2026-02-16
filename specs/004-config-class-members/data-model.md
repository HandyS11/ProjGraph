# Data Model: Class Member Visibility

## Entity: AnalysisOptions (Extension)

The `AnalysisOptions` class in `ProjGraph.Lib.ClassDiagram` will be extended with the following fields:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| IncludeProperties | bool | true | When true, includes properties and fields in the type definition. |
| IncludeFunctions | bool | true | When true, includes methods in the type definition. Constructors are not currently extracted by the analyzer and are out of scope. |

## Entity: MemberDefinition (Reference)

Existing entity in `ProjGraph.Core.Models`.

| Field | Type | Category mapping |
|-------|------|------------------|
| Kind | MemberKind | Property, Field -> "Properties" |
| Kind | MemberKind | Method -> "Functions" |

Note: The `MemberKind` enum contains `Field = 0`, `Property = 1`, `Method = 2`. There is no `Constructor` value — constructors are not extracted by `TypeAnalyzer` and are out of scope.

## Validation Rules

1. Mapping for `MemberKind`:
   - `MemberKind.Property` and `MemberKind.Field` MUST be filtered by `IncludeProperties`.
   - `MemberKind.Method` MUST be filtered by `IncludeFunctions`.
2. Discovery Rule: Hiding a property MUST NOT prevent the identification of an Association relationship if the property's type is a local or workspace type.
