#!/usr/bin/env bash
# Stamps <version> into every "version" field in src/ProjGraph.Mcp/.mcp/server.json: the top-level
# server version and the packaged ProjGraph.Mcp version. Every ProjGraph.Mcp* package embeds this
# file, so it must run before packing, not only once in publish.
# Usage: set-server-json-version.sh <version>
# Keep it Bash 3.2 compatible: it also runs on macOS (osx-arm64 pack job).
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <version>" >&2
  exit 2
fi

version=$1
file=src/ProjGraph.Mcp/.mcp/server.json

# -i.bak works on both GNU sed (Linux, Windows Git Bash) and BSD sed (macOS); [^\"]* replaces
# GNU-only \+ so the pattern matches on both too.
sed -i.bak "s/\"version\": \"[^\"]*\"/\"version\": \"$version\"/g" "$file" && rm "$file.bak"

echo "Set server.json version to $version:"
grep '"version"' "$file"

count=$(grep -c "\"version\": \"$version\"" "$file")
if [ "$count" -ne 2 ]; then
  echo "::error::Expected 2 occurrences of \"version\": \"$version\" in $file, found $count." >&2
  exit 1
fi
