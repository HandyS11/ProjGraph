# MCP Roots Contract

**Date**: 2026-03-10
**Branch**: `012-mcp-integration-primitives`
**SDK**: `McpServer.RequestRootsAsync()` / `server.ClientCapabilities?.Roots`

---

## Overview

MCP Roots is a **client-declared** capability. The server does not advertise roots — it _requests_ them from the client. This allows the server to resolve relative file paths against the user's declared workspace directories.

---

## Capability Detection

At the start of the first tool call, `WorkspaceRootService` checks whether the client supports Roots:

```none
Client Capabilities → ClientCapabilities.Roots
  ├── null         → RootsStatus.Unsupported (client does not support roots)
  └── not null     → RequestRootsAsync() → RootsStatus.Ready(List<WorkspaceRoot>)
```

---

## Sequence: First Tool Call with Relative Path

```none
Client                            Server (WorkspaceRootService)
  │                                      │
  │── tool call ("MySolution.slnx") ────>│
  │                                      │
  │                              Check ClientCapabilities.Roots
  │                                      │
  │<── roots/list request ───────────────│   (if supported)
  │── roots/list response ──────────────>│
  │                                      │
  │                              Cache roots: [{ uri: "file:///D:/Projects", name: "MyApp" }]
  │                                      │
  │                              Resolve: "MySolution.slnx" → "D:\Projects\MySolution.slnx"
  │                                      │
  │                              Execute tool with resolved path
  │<── tool result ──────────────────────│
```

---

## Sequence: Client Changes Workspace Roots

```none
Client                            Server (WorkspaceRootService)
  │                                      │
  │── notifications/roots/list_changed ─>│
  │                                      │
  │<── roots/list request ───────────────│
  │── roots/list response ──────────────>│
  │                                      │
  │                              Update cached roots
```

---

## Path Resolution Rules

| Input Path | Behavior                                                                                                |
|-----------|---------------------------------------------------------------------------------------------------------|
| Absolute path (e.g., `D:\Projects\My.slnx`) | Passed through unchanged — no root lookup                                                               |
| Relative path, roots supported, exactly 1 match | Resolved to absolute path                                                                               |
| Relative path, roots supported, 0 matches | `FileNotFoundException`: "File '{name}' not found under any workspace root"                             |
| Relative path, roots supported, 2+ matches | `AmbiguousMatchException`: "'{name}' matches multiple roots: {list}. Provide an absolute path."         |
| Relative path, roots not supported | `InvalidOperationException`: "Client does not support workspace roots. Please provide an absolute path." |
| Relative path, roots not yet fetched | Fetch roots first (lazy init), then apply rules above                                                   |

---

## `Root` Object (from SDK)

| Field | Type | Description                                      |
|-------|------|--------------------------------------------------|
| `Uri` | `string` | Absolute URI (e.g., `file:///D:/Projects/MyApp`) |
| `Name` | `string?` | Optional label                                   |

Local path is derived from `Uri` via `new Uri(root.Uri).LocalPath`.

---

## Graceful Degradation

When the client does NOT support Roots:

- No error or warning at server startup or initialization.
- Relative paths passed to tools will receive a clear error message only when used.
- Absolute paths work exactly as before — no change in behavior.
- The server does NOT crash, warn, or behave differently until a relative path is actually attempted.

---

## Implementation Notes

- **Service**: `WorkspaceRootService` in `src/ProjGraph.Mcp/WorkspaceRootService.cs`
- **Singleton**: registered in `Program.cs` with `builder.Services.AddSingleton<WorkspaceRootService>()`
- **Injection into tools**: Added to `ProjGraphTools` constructor alongside existing services
- **Lazy init**: First call to `TryResolveAsync` initializes the roots by calling `server.RequestRootsAsync()`
- **Notification handler**: Registered in `WorkspaceRootService` constructor or via `Program.cs` after server builds; uses `server.RegisterNotificationHandler(NotificationMethods.RootsListChangedNotification, ...)`
- **Thread safety**: `SemaphoreSlim(1,1)` on initialization to prevent concurrent root fetch races

---

## Out of Scope

- Auto-discovering solution files within roots (not specified — user must still name the file)
- Watching root directories for file changes
- Roots subscription or server-side push of root changes
