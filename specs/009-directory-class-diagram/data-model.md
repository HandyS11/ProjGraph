# Data Model & Technical Design: Folder/Directory Scanning

## Overview

The feature introduces a unified analysis flow for both individual C# files and entire directories. The design leverages Roslyn's ability to process multiple syntax trees in a single compilation.

## Logic Flow: Directory Analysis

1. **Validation**: Verify the provided path is a valid directory.
2. **Discovery**:
    - Recursively scan the directory for `.cs` files.
    - Path exclusion: Skip any directory where `DirectoryFilters.ShouldSkipDirectory(path)` returns true (e.g., `bin`, `obj`, `.git`).
    - Collection: Maintain a list of absolute paths to discovered files.
3. **Threshold Check**:
    - If the number of discovered files exceeds 50, log a warning to `IOutputConsole`.
    - If no `.cs` files are found, return a successful but empty `ClassModel`.
4. **Parsing**:
    - For each file, read its content (using `IFileSystem`).
    - Create a `SyntaxTree` for each file using `CSharpSyntaxTree.ParseText`.
5. **Compilation**:
    - Create a single `CSharpCompilation` containing ALL discovered syntax trees using `ICompilationFactory`.
6. **Analysis**:
    - Initialize `AnalysisContext`.
    - Enqueue ALL types found in ALL syntax trees into the `typesToAnalyze` queue.
    - Run `typeProcessor.ProcessTypeQueueAsync` as usual.
7. **Output**:
    - Return a `ClassModel` with the directory name as the title.

## Use Cases

### DiscoverCsFilesUseCase

- Responsible for the recursive file system traversal.
- Input: `string directoryPath`.
- Output: `IEnumerable<string> filePaths`.

### AnalyzeDirectoryUseCase

- Responsible for orchestrating directory discovery and multi-file analysis.
- Input: `string directoryPath, AnalysisOptions options`.
- Output: `ClassModel`.

## Updated Services

### IClassAnalysisService

- `Task<ClassModel> AnalyzeDirectoryAsync(string directoryPath, AnalysisOptions? options = null);`
- Implementation maps to `AnalyzeDirectoryUseCase`.
