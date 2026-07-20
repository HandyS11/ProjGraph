using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.IO.Pipelines;

namespace ProjGraph.Tests.Integration.Mcp.Helpers;

/// <summary>
/// Hosts a real <see cref="McpServer"/> and a real <see cref="McpClient"/> connected to each other
/// over an in-memory duplex pipe pair. Unlike <c>McpTestHelper</c> (which hand-wires the tools with
/// a <see langword="null"/> server), this gives tests a live <see cref="McpServer"/> — with a
/// resource collection, client capabilities established by the initialize handshake, and working
/// server→client requests/notifications — without spawning the server process.
/// </summary>
internal sealed class InProcessMcpSession : IAsyncDisposable
{
    private readonly Task _serverLoop;

    private InProcessMcpSession(McpServer server, McpClient client, Task serverLoop)
    {
        Server = server;
        Client = client;
        _serverLoop = serverLoop;
    }

    public McpServer Server { get; }

    public McpClient Client { get; }

    public static async Task<InProcessMcpSession> StartAsync(
        McpServerOptions? serverOptions = null,
        McpClientOptions? clientOptions = null)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var serverTransport = new StreamServerTransport(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "in-process");

        var server = McpServer.Create(serverTransport, serverOptions ?? CreateServerOptions());
        var serverLoop = server.RunAsync(CancellationToken.None);

        var clientTransport = new StreamClientTransport(
            clientToServer.Writer.AsStream(),
            serverToClient.Reader.AsStream());

        var client = await McpClient.CreateAsync(clientTransport, clientOptions);
        return new InProcessMcpSession(server, client, serverLoop);
    }

    /// <summary>
    /// Server options mirroring how <c>Program</c> configures the real server: a mutable resource
    /// collection plus the resource capabilities the diagram cache relies on.
    /// </summary>
    public static McpServerOptions CreateServerOptions()
    {
        return new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "ProjGraph.Tests",
                Version = "1.0.0"
            },
            ResourceCollection = [],
            Capabilities = new ServerCapabilities
            {
                Resources = new ResourcesCapability
                {
                    ListChanged = true,
                    Subscribe = true
                }
            }
        };
    }

    /// <summary>
    /// Client options advertising the workspace-roots capability and serving the roots produced by
    /// <paramref name="rootProvider"/>, which is re-invoked on every <c>roots/list</c> request so a
    /// test can change the roots mid-session.
    /// </summary>
    /// <param name="rootProvider">Supplies the workspace root directories for each request.</param>
    /// <returns>Client options that answer <c>roots/list</c> from <paramref name="rootProvider"/>.</returns>
    public static McpClientOptions CreateClientOptionsWithRoots(Func<IReadOnlyList<string>> rootProvider)
    {
        return new McpClientOptions
        {
            ClientInfo = new Implementation
            {
                Name = "ProjGraph.Tests",
                Version = "1.0.0"
            },
            Capabilities = new ClientCapabilities
            {
                Roots = new RootsCapability
                {
                    ListChanged = true
                }
            },
            Handlers = new McpClientHandlers
            {
                RootsHandler = (_, _) => ValueTask.FromResult(new ListRootsResult
                {
                    Roots = [.. rootProvider().Select(directory => new Root
                    {
                        Uri = new Uri(directory).AbsoluteUri
                    })]
                })
            }
        };
    }

    /// <summary>
    /// Client options that deliberately advertise no capabilities at all — in particular no
    /// workspace roots, so relative paths cannot be resolved.
    /// </summary>
    public static McpClientOptions CreateClientOptionsWithoutRoots()
    {
        return new McpClientOptions
        {
            ClientInfo = new Implementation
            {
                Name = "ProjGraph.Tests",
                Version = "1.0.0"
            },
            Capabilities = new ClientCapabilities()
        };
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await Server.DisposeAsync();

        // The loop ends once the transport is torn down. Observe its outcome without awaiting it,
        // so an expected teardown fault is not raised later as an unobserved task exception.
        _ = _serverLoop.ContinueWith(static loop => _ = loop.Exception, TaskScheduler.Default);
    }
}
