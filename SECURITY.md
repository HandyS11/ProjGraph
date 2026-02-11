# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in ProjGraph, please report it responsibly:

1. **Do not** open a public GitHub issue for security vulnerabilities.
2. Email [HandyS11](https://github.com/HandyS11) or use
   GitHub's [private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
   feature.
3. Include a clear description and, if possible, steps to reproduce.

## Scope

ProjGraph is a developer tooling project that:

- Reads and parses `.sln`, `.slnx`, `.csproj`, and `.cs` files from the local filesystem.
- Does **not** execute user code — it uses Roslyn for static analysis only.
- Does **not** make network requests (except NuGet restore during build).
- The MCP server communicates via stdio (JSON-RPC), not over a network.

Security concerns are most likely to involve path traversal or unexpected file access rather than remote exploitation.
