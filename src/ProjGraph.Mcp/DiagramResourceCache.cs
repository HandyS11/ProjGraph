using ModelContextProtocol;
using ModelContextProtocol.Protocol;
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
        var resourceCollection = server?.ServerOptions.ResourceCollection;

        McpServerResource? newResource = null;
        McpServerResource? evictedResource = null;
        bool isUpdate;

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

                isUpdate = true;
            }
            else
            {
                isUpdate = false;

                // Evict if at capacity
                if (_entries.Count >= MaxCachedResources)
                {
                    var lruUri = _lruOrder.Last!.Value;
                    evictedResource = _entries[lruUri].ServerResource;
                    _lruOrder.RemoveLast();
                    _entries.Remove(lruUri);
                }

                // Create a concrete McpServerResource so this entry appears in resources/list
                var filename = Path.GetFileName(sourcePath);
                var entryName = $"{type} — {filename}";
                if (resourceCollection is not null)
                {
                    newResource = McpServerResource.Create(
                        (Func<ReadResourceResult>)(() => ReadForResource(uri)),
                        new McpServerResourceCreateOptions
                        {
                            UriTemplate = uri,
                            Name = entryName,
                            Description = description,
                            MimeType = mimeType
                        });
                }

                // Add new entry
                var node = _lruOrder.AddFirst(uri);
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
                    Name = entryName,
                    ServerResource = newResource
                };
            }
        }

        // Outside the lock: mutate the server's resource collection.
        // McpServerPrimitiveCollection.Add/Remove automatically fire notifications/resources/list_changed.
        if (evictedResource is not null)
        {
            resourceCollection?.Remove(evictedResource);
        }

        if (newResource is not null)
        {
            resourceCollection?.Add(newResource);
        }
        else if (isUpdate && server is not null)
        {
            // Notify clients that the resource content has changed (URI stays the same)
            try
            {
                await server.SendNotificationAsync(
                    NotificationMethods.ResourceUpdatedNotification,
                    new ResourceUpdatedNotificationParams
                    {
                        Uri = uri
                    },
                    null,
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
            if (!_entries.TryGetValue(uri, out var entry))
            {
                return null;
            }

            // Update LRU order on read: move this entry to the front
            _lruOrder.Remove(entry.LruNode);
            _lruOrder.AddFirst(entry.LruNode);
            return entry.Content;
        }
    }

    public IReadOnlyList<DiagramResource> ListResources()
    {
        lock (_lock)
        {
            return
            [
                .. _entries.Values
                    .Select(e => new DiagramResource(e.Uri, e.Name, e.Description, e.MimeType, e.AnalysisType,
                        e.SourcePath, e.GeneratedAt, e.LastUpdatedAt))
            ];
        }
    }

    private ReadResourceResult ReadForResource(string uri)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(uri, out var entry))
            {
                throw new McpException($"Resource not found: {uri}");
            }

            // Update LRU order on read
            _lruOrder.Remove(entry.LruNode);
            _lruOrder.AddFirst(entry.LruNode);

            return new ReadResourceResult
            {
                Contents =
                [
                    new TextResourceContents
                    {
                        Uri = entry.Uri,
                        MimeType = entry.MimeType,
                        Text = entry.Content
                    }
                ]
            };
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
        public McpServerResource? ServerResource { get; init; }
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
