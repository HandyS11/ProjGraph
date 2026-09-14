#!/usr/bin/env bash
# Waits until NuGet.org's flat container lists a version for every package ID, so a pointer package
# is pushed only once every package it points to can be installed.
# Usage: wait-for-nuget.sh <version> <package-id>...
# NUGET_WAIT_TIMEOUT_SECONDS (default 1800) and NUGET_WAIT_INTERVAL_SECONDS (default 30) set the timing.
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <version> <package-id>..." >&2
  exit 2
fi

version=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
shift
timeout=${NUGET_WAIT_TIMEOUT_SECONDS:-1800}
interval=${NUGET_WAIT_INTERVAL_SECONDS:-30}
deadline=$((SECONDS + timeout))
pending=("$@")

while :; do
  waiting=()
  for id in "${pending[@]}"; do
    lower_id=$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')
    if curl --silent --fail "https://api.nuget.org/v3-flatcontainer/$lower_id/index.json" |
      jq --exit-status --arg version "$version" '.versions | index($version) != null' > /dev/null; then
      echo "$id $version is listed on NuGet.org."
    else
      waiting+=("$id")
    fi
  done

  if [ "${#waiting[@]}" -eq 0 ]; then
    exit 0
  fi

  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "::error::Timed out after ${timeout}s waiting for $version of: ${waiting[*]}" >&2
    exit 1
  fi

  echo "Waiting ${interval}s for: ${waiting[*]}"
  sleep "$interval"
  pending=("${waiting[@]}")
done
