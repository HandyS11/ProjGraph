#!/usr/bin/env bash
# Fails unless a directory holds exactly the .nupkg files of the named package sets.
# Usage: verify-packages.sh <directory> <version> <set>...
# Sets: libraries, pointers, any, win-x64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64, osx-arm64.
# Keep it Bash 3.2 compatible: the osx-arm64 pack job runs it on macOS.
set -euo pipefail

if [ "$#" -lt 3 ]; then
  echo "usage: $0 <directory> <version> <set>..." >&2
  exit 2
fi

directory=$1
version=$2
shift 2

expected=""
add() {
  expected="$expected$1.$version.nupkg"$'\n'
}

for set in "$@"; do
  case "$set" in
    libraries)
      for id in ProjGraph.Core ProjGraph.Lib ProjGraph.Lib.Core ProjGraph.Lib.Dependencies \
                ProjGraph.Lib.ClassDiagram ProjGraph.Lib.EntityFramework; do
        add "$id"
      done
      ;;
    pointers)
      add ProjGraph.Cli
      add ProjGraph.Mcp
      ;;
    any | win-x64 | linux-x64 | linux-arm64 | linux-musl-x64 | linux-musl-arm64 | osx-arm64)
      add "ProjGraph.Cli.$set"
      add "ProjGraph.Mcp.$set"
      ;;
    *)
      echo "::error::Unknown package set '$set'." >&2
      exit 2
      ;;
  esac
done

expected=$(printf '%s' "$expected" | sort)
actual=$(for package in "$directory"/*.nupkg; do
  if [ -e "$package" ]; then basename "$package"; fi
done | sort)

if [ "$actual" != "$expected" ]; then
  echo "::error::$directory doesn't hold exactly the packages for: $*" >&2
  diff <(printf '%s\n' "$expected") <(printf '%s\n' "$actual") >&2 || true
  exit 1
fi

echo "$directory holds the $(printf '%s\n' "$actual" | wc -l | tr -d ' ') expected packages."
