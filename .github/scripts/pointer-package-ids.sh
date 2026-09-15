#!/usr/bin/env bash
# Prints the ID of every package the pointer packages in a directory list, one per line, sorted and
# unique. A pointer package lists them in tools/any/any/DotnetToolSettings.xml, as one
# <RuntimeIdentifierPackage RuntimeIdentifier="<rid>" Id="<id>" /> element per runtime identifier.
# Usage: pointer-package-ids.sh <pointers-directory>
# Keep it Bash 3.2 compatible, like the other packaging scripts.
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <pointers-directory>" >&2
  exit 2
fi

directory=$1
settings_path=tools/any/any/DotnetToolSettings.xml
ids=""
packages=0

for package in "$directory"/*.nupkg; do
  if [ ! -e "$package" ]; then
    continue
  fi
  packages=$((packages + 1))

  if ! settings=$(unzip -p "$package" "$settings_path" 2> /dev/null) || [ -z "$settings" ]; then
    echo "::error::$package has no $settings_path." >&2
    exit 1
  fi

  # Joining the lines first keeps an element that spans several lines matchable.
  package_ids=$(printf '%s\n' "$settings" | tr -s '[:space:]' ' ' |
    grep -o '<RuntimeIdentifierPackage [^>]*>' |
    grep -o '[[:space:]]Id="[^"]*"' |
    sed 's/^[[:space:]]Id="//; s/"$//' || true)
  if [ -z "$package_ids" ]; then
    echo "::error::$package lists no RuntimeIdentifierPackage IDs in $settings_path." >&2
    exit 1
  fi

  ids="$ids$package_ids"$'\n'
done

if [ "$packages" -eq 0 ]; then
  echo "::error::$directory holds no .nupkg files." >&2
  exit 1
fi

printf '%s' "$ids" | LC_ALL=C sort -u
