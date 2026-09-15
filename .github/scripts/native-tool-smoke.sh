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

# The tools resolve from the local packages: no ProjGraph package with this version is on NuGet.org
# before a release pushes it, and a fresh package folder keeps a cached copy of the same version from
# being installed instead. NuGet.org is listed for the SDK itself: on macOS arm64, `dotnet tool install`
# downloads microsoft.netcore.app.host.osx-x64 for any RID-specific package (its tools/any folder reads
# as a pre-net6 framework), even though a native tool gets a symlink rather than an apphost shim.
cat > artifacts/smoke-nuget.config <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="packages" value="packages" />
    <add key="pointers" value="pointers" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
XML
rm -rf artifacts/tools artifacts/tool-install-packages
for tool in Cli Mcp; do
  NUGET_PACKAGES="$PWD/artifacts/tool-install-packages" dotnet tool install "ProjGraph.$tool" --version "$version" \
    --configfile artifacts/smoke-nuget.config --tool-path "artifacts/tools/$tool"
done

# Prints the one file named $2 inside the package store of the tool path $1, and fails otherwise.
store_executable() {
  found=''
  for candidate in "$1"/.store/*/*/*/*/tools/*/*/"$2"; do
    if [ -f "$candidate" ]; then
      if [ -n "$found" ]; then
        echo "::error::More than one $2 in $1/.store." >&2
        exit 1
      fi
      found=$candidate
    fi
  done
  if [ -z "$found" ]; then
    echo "::error::No $2 in $1/.store." >&2
    exit 1
  fi
  printf '%s\n' "$found"
}

cli_native=artifacts/tools/Cli/projgraph
mcp_native=artifacts/tools/Mcp/ProjGraph.Mcp
mcp_reference=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll
if [ "${RUNNER_OS:-}" = 'Windows' ]; then
  # On Windows the tool path holds .cmd launchers, not links, so the smoke suite runs the executables
  # the packages installed into the tool store.
  cli_native=$(store_executable artifacts/tools/Cli ProjGraph.Cli.exe)
  mcp_native=$(store_executable artifacts/tools/Mcp ProjGraph.Mcp.exe)
  # The MCP SDK starts stdio servers through `cmd.exe /c` on Windows, which is fragile with a quoted
  # dotnet host path. The JIT apphost runs the same build without one.
  mcp_reference=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.exe
fi

PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE="$cli_native" \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE="$mcp_native" \
PROJGRAPH_SMOKE_MCP_REFERENCE="$mcp_reference" \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build --configuration Release \
  --logger "console;verbosity=normal" --logger "trx;LogFileName=smoke.trx" \
  --results-directory artifacts/smoke-results
