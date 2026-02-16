# Research: Class Member Visibility Configuration

## Decision 1: Mapping Roslyn symbols to categories

- **Decision**: `PropertySymbol`, `FieldSymbol` -> **Properties**. `MethodSymbol` (ordinary methods only) -> **Functions**.
- **Rationale**: User clarified that Fields should be grouped with Properties. Constructors are not currently extracted by the analyzer (`TypeAnalyzer` only captures `MethodKind.Ordinary`) and are out of scope for this feature.
- **Alternatives Considered**: Separate toggles for each, but rejected for simplicity as per user request.

## Decision 2: Implementation Point for Filtering

- **Decision**: Filter during the **Analysis** phase inside `TypeAnalyzer.AnalyzeType`, before building the `TypeDefinition` model.
- **Rationale**: By filtering members in `TypeAnalyzer`, the `TypeDefinition` model arrives pre-filtered to the renderer, making `MermaidClassDiagramRenderer` unchanged. Dependency discovery (Association relationships) must still scan all member types *before* filtering, so `TypeProcessor.DiscoverRelatedTypes` runs on the full symbol regardless of visibility flags. This satisfies both FR-004 (discovery unaffected) and SC-003 (less data to render).

## Decision 3: MCP Schema Update

- **Decision**: Add `includeProperties` and `includeFunctions` as boolean parameters with default `true`.
- **Rationale**: Ensures backward compatibility and satisfies FR-003.

## Findings from Codebase

- `ProjGraph.Lib.ClassDiagram.Application.AnalysisOptions` needs 2 new fields.
- `ProjGraph.Mcp.Program.GetClassDiagramAsync` needs to pass these new options.
- `ProjGraph.Lib.ClassDiagram.Rendering.MermaidClassDiagramRenderer` requires no changes (receives pre-filtered `TypeDefinition` models).

### Current Implementation Exploration

- `ClassAnalysisService` uses Roslyn to populate `TypeDefinition`.
- `TypeDefinition` contains `IReadOnlyList<MemberDefinition>`.
- `MemberDefinition` has `MemberKind` (Field, Property, Method).
- `MermaidClassDiagramRenderer` iterates over members in `RenderType`.
- `DiagramOptions` (in `ProjGraph.Lib.Core`) is general, while `AnalysisOptions` (in `ProjGraph.Lib.ClassDiagram`) is specific to this tool.

### Proposed Changes

1. **Update `AnalysisOptions`**: Add `IncludeProperties` and `IncludeFunctions` (both default `true`).
2. **Update `TypeAnalyzer.AnalyzeType`**: Accept `includeProperties` and `includeFunctions` flags. Skip adding members to `TypeDefinition` based on these flags.
    - *Caution*: Dependency discovery in `TypeProcessor.DiscoverRelatedTypes` must still scan all symbol members for relationship detection, even if `IncludeProperties` is false. The filtering only affects what goes into `TypeDefinition.Members`.
3. **Update `ProjGraph.Mcp`**: Expose these parameters in `GetClassDiagramAsync` and pass them into the `AnalysisOptions`.
4. **No changes to `MermaidClassDiagramRenderer`** might be needed if `TypeDefinition` already comes filtered from the service. This is cleaner and more performant.
