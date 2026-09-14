#!/usr/bin/env bash
# Builds the solution, packs the Native AOT ProjGraph.Cli and ProjGraph.Mcp tool packages for one
# runtime identifier, installs both tools from those packages the way users do, and runs
# tests/ProjGraph.Tests.Smoke.Aot against the JIT build of the same commit.
# Usage: native-tool-smoke.sh <rid> <version>
# Run it from the repository root, on an OS that matches the RID (Alpine for linux-musl-*).
# Leaves the RID-specific packages in artifacts/packages, the pointer packages in artifacts/pointers,
# and the test results in artifacts/smoke-results.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 <rid> <version>" >&2
  exit 2
fi

rid=$1
version=$2

dotnet restore ProjGraph.slnx
dotnet build ProjGraph.slnx --no-restore --configuration Release -p:Version="$version"

# The pointer packages are only needed to install the tools below; a release publishes the ones
# from the pack-portable job.
dotnet pack src/ProjGraph.Cli --no-build --configuration Release -p:Version="$version" --output artifacts/pointers
dotnet pack src/ProjGraph.Mcp --no-build --configuration Release -p:Version="$version" --output artifacts/pointers

# One native pack at a time: each ILC run takes several GB of memory.
dotnet pack src/ProjGraph.Cli --configuration Release --runtime "$rid" -p:Version="$version" --output artifacts/packages
dotnet pack src/ProjGraph.Mcp --configuration Release --runtime "$rid" -p:Version="$version" --output artifacts/packages

# Only the local packages are sources, so nothing resolves from NuGet.org, and a fresh package folder
# keeps a cached copy of the same version from being installed instead.
cat > artifacts/smoke-nuget.config <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="packages" value="packages" />
    <add key="pointers" value="pointers" />
  </packageSources>
</configuration>
XML
rm -rf artifacts/tools artifacts/tool-install-packages
for tool in Cli Mcp; do
  NUGET_PACKAGES="$PWD/artifacts/tool-install-packages" dotnet tool install "ProjGraph.$tool" --version "$version" \
    --configfile artifacts/smoke-nuget.config --tool-path "artifacts/tools/$tool"
done

exe=''
mcp_reference=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll
if [ "${RUNNER_OS:-}" = 'Windows' ]; then
  exe='.exe'
  # The MCP SDK starts stdio servers through `cmd.exe /c` on Windows, which is fragile with a quoted
  # dotnet host path. The JIT apphost runs the same build without one.
  mcp_reference=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.exe
fi

PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE="artifacts/tools/Cli/projgraph$exe" \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE="artifacts/tools/Mcp/ProjGraph.Mcp$exe" \
PROJGRAPH_SMOKE_MCP_REFERENCE="$mcp_reference" \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build --configuration Release \
  --logger "console;verbosity=normal" --logger "trx;LogFileName=smoke.trx" \
  --results-directory artifacts/smoke-results
