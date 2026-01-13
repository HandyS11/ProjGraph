# Quickstart: ProjGraph

## CLI Usage

1. **Install the tool**:
   ```bash
   dotnet tool install -g ProjGraph.Cli
   ```

2. **Visualize a solution**:
   ```bash
   projgraph visualize ./MySolution.sln
   ```

3. **Export to Mermaid**:
   ```bash
   projgraph visualize ./MyProject.csproj --format mermaid
   ```

## MCP Integration (GitHub Copilot / Claude)

1. **Configure the client**:
   Add ProjGraph to your `mcpConfig.json`:
   ```json
   {
     "mcpServers": {
       "projgraph": {
         "command": "projgraph-mcp",
         "args": []
       }
     }
   }
   ```

2. **Run a query**:
   Ask your AI agent:
   > "Show me the project dependencies for C:\Code\MySolution.sln"
