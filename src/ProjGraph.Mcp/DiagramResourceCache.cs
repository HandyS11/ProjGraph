using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ProjGraph.Mcp;

internal sealed class DiagramResourceCache
{
    public const int MaxCachedResources = 50;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, CacheEntry> _entries = [];
    private readonly LinkedList<string> _lruOrder = [];

    public async Task StoreAsync(string type, string sourcePath, string mimeType, string content,
        string description, McpServer? server, CancellationToken ct)
    {
        var uri = $"projgraph://diagrams/{type}/{Uri.EscapeDataString(sourcePath)}";
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (_entries.TryGetValue(uri, out var existing))
            {
                // Update existing entry
                existing.Content = content;
                existing.MimeType = mimeType;
                existing.Description = description;
                existing.LastUpdatedAt = now;

                // Move to front of LRU
                _lruOrder.Remove(existing.LruNode);
                _lruOrder.AddFirst(existing.LruNode);
            }
            else
            {
                // Evict if at capacity
                if (_entries.Count >= MaxCachedResources)
                {
                    var lruUri = _lruOrder.Last!.Value;
                    _lruOrder.RemoveLast();
                    _entries.Remove(lruUri);
                }

                // Add new entry
                var node = _lruOrder.AddFirst(uri);
                var filename = Path.GetFileName(sourcePath);
                _entries[uri] = new CacheEntry
                {
                    Uri = uri,
                    AnalysisType = type,
                    SourcePath = sourcePath,
                    MimeType = mimeType,
                    Content = content,
                    Description = description,
                    GeneratedAt = now,
                    LastUpdatedAt = now,
                    LruNode = node,
                    Name = $"{type} — {filename}"
                };
            }
        }

        // Send notification outside the lock
        if (server is not null)
        {
            try
            {
                await server.SendNotificationAsync(
                    "notifications/resources/list_changed",
                    ct);
            }
            catch (McpException)
            {
                // Client may not support notifications — ignore
            }
        }
    }

    public string? TryRead(string uri)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(uri, out var entry))
            {
                // Update LRU order on read: move this entry to the front
                _lruOrder.Remove(entry.LruNode);
                _lruOrder.AddFirst(entry.LruNode);
                return entry.Content;
            }

            return null;
        }
    }

    public IReadOnlyList<DiagramResource> ListResources()
    {
        lock (_lock)
        {
            return _entries.Values
                .Select(e => new DiagramResource(e.Uri, e.Name, e.Description, e.MimeType, e.AnalysisType,
                    e.SourcePath, e.GeneratedAt, e.LastUpdatedAt))
                .ToList();
        }
    }

    private sealed class CacheEntry
    {
        public required string Uri { get; init; }
        public required string AnalysisType { get; init; }
        public required string SourcePath { get; init; }
        public required string MimeType { get; set; }
        public required string Content { get; set; }
        public required string Description { get; set; }
        public required string Name { get; init; }
        public required DateTimeOffset GeneratedAt { get; init; }
        public required DateTimeOffset LastUpdatedAt { get; set; }
        public required LinkedListNode<string> LruNode { get; init; }
    }
}

internal sealed record DiagramResource(
    string Uri,
    string Name,
    string Description,
    string MimeType,
    string AnalysisType,
    string SourcePath,
    DateTimeOffset GeneratedAt,
    DateTimeOffset LastUpdatedAt);
