# Feature Specification: File Output (`--output` flag)

**Feature Branch**: `007-cli-file-output`
**Created**: 2026-02-24
**Status**: Draft
**Input**: User description: "Add -o|--output <file> to all CLI commands to write the diagram directly to disk instead of stdout. The current workflow requires `projgraph visualize ... > diagram.mmd` which is fragile on Windows PowerShell (BOM issues, encoding). A built-in flag is more reliable and enables easier CI integration. Each `Command` reads `OutputPath` from settings and calls `File.WriteAllTextAsync` after rendering."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Save diagram to file (Priority: P1)

As a user, I want to save the generated diagram directly to a file using a CLI flag so that I can avoid shell redirection issues like BOM or encoding errors on Windows PowerShell.

**Why this priority**: Core value of the feature. Shell redirection is fragile on some platforms, and a built-in flag is the standard way to handle file output in CLI tools.

**Independent Test**: Run `projgraph visualize <path> --output diagram.mmd` and verify that `diagram.mmd` is created with the correct content and no unwanted encoding issues.

**Acceptance Scenarios**:

1. **Given** a valid project path, **When** I run the visualize command with `-o output.mmd`, **Then** the diagram is saved to `output.mmd` and not printed to stdout.
2. **Given** an invalid output path (e.g., read-only directory), **When** I run the command with `--output`, **Then** the CLI should report a clear error message.

---

### User Story 2 - CI/CD Integration (Priority: P2)

As a developer, I want to use the `--output` flag in my build scripts or CI pipelines to generate documentation artifacts reliably.

**Why this priority**: Enables automation and improves the developer experience for documentation generation.

**Independent Test**: Running the command in a github action or script and verifying the exit code is 0 and the file exists.

**Acceptance Scenarios**:

1. **Given** a CI script, **When** I execute `projgraph erd <path> --output docs/erd.mmd`, **Then** the file is created in the specified directory.

---

### User Story 3 - Feedback on success (Priority: P2)

As a user, I want to receive confirmation that the file has been written successfully.

**Why this priority**: Good UX; users should know exactly what happened and where the file is.

**Independent Test**: Verify that a "Saved to <path>" message appears in the console output (stderr/info) when using the flag.

**Acceptance Scenarios**:

1. **Given** the `--output` flag is used, **When** the operation completes, **Then** an informational message stating "Saved to <file>" is displayed.

### Edge Cases

- **File already exists**: Should the CLI overwrite without asking (common for CLI tools) or should it warn? I'll assume it overwrites by default.
- **Directory doesn't exist**: Should the CLI create the directory or fail? I'll assume it should try to create it.
- **Permission denied**: The CLI should report it clearly.
- **Very large output**: Ensure the file writing is handled correctly without running out of memory (not expected to be an issue for Mermaid diagrams).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `-o|--output <file>` option for `visualize`, `classdiagram`, and `erd` commands.
- **FR-002**: System MUST write the rendered diagram to the specified file path if the output option is provided.
- **FR-003**: System MUST NOT write the diagram to stdout when the output option is provided.
- **FR-004**: System MUST ensure the output file is written using consistent encoding (UTF-8 without BOM).
- **FR-005**: System MUST report an error if the specified output file cannot be written (e.g., permission denied, invalid path).
- **FR-006**: System MUST display a success confirmation message upon successfully writing the file.
- **FR-007**: System MUST automatically wrap diagram output in a Markdown code fence if the output file extension indicates a Markdown environment (e.g., `.md`), unless the extension is `.mmd`.
- **FR-008**: System MUST attempt to create the parent directory if it does not exist before writing the output file.

### Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of CLI commands (`visualize`, `classdiagram`, `erd`) correctly handle the `--output` flag.
- **SC-002**: Diagrams saved via `--output` contain the same diagram logic as stdout, with optional Markdown fencing as per `FR-007`.
- **SC-003**: Output files are consistently encoded as UTF-8 without BOM.
- **SC-004**: Command returns a non-zero exit code if file writing fails.

## Assumptions

- We will use `IFileSystem` for file writing to maintain testability.
- If the directory for the output file doesn't exist, we might or might not want to create it. (I'll assume we should try to create it or at least report it).
- We will use UTF-8 without BOM as it is the standard for most modern tools, especially for Mermaid diagrams.
