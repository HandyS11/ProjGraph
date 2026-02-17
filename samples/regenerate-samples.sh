#!/bin/bash

# SYNOPSIS
#     Regenerates all Mermaid snapshots for the samples showcase.
#     This script ensures that the documentation is always up-to-date with the latest ProjGraph CLI output.

# Stop on error
set -e

# Get the script directory and root directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

# CD into root to make paths relative for dotnet
cd "$ROOT"
CLI_PROJECT="./src/ProjGraph.Cli/ProjGraph.Cli.csproj"

invoke_projgraph() {
    local args=$1
    local output_path="$2"
    
    # Make output path relative if it's within ROOT
    # This ensures we handle the path correctly regardless of where we are
    local output_rel="${output_path#"$ROOT"/}"

    echo -e "\033[0;36mRendering: $output_rel\033[0m"
    
    # Use dotnet or dotnet.exe
    DOTNET_CMD="dotnet"
    if ! command -v dotnet &> /dev/null; then
        if command -v dotnet.exe &> /dev/null; then
            DOTNET_CMD="dotnet.exe"
        fi
    fi

    # Run the CLI tool and redirect output
    # Always execute from ROOT to ensure relative project paths in $args match
    if (cd "$ROOT" && $DOTNET_CMD run --project "$CLI_PROJECT" --no-build -- "$args" > "$output_rel"); then
        echo -e "\033[0;32mSuccessfully generated snapshot: $(basename "$output_path")\033[0m"
    else
        echo -e "\033[0;31mFailed to generate snapshot for: $args\033[0m"
        exit 1
    fi
}

echo -e "\033[44;37m--- ProjGraph Samples Regeneration ---\033[0m"

# 1. ERD: Complex E-commerce
invoke_projgraph \
    "erd ./samples/erd/complex-ecommerce/Data/MyDbContext.cs" \
    "$ROOT/samples/erd/complex-ecommerce/complex-ecommerce.mmd"

# 2. ERD: Simple Context
invoke_projgraph \
    "erd ./samples/erd/simple-context/EntityFramework/MyDbContext.cs" \
    "$ROOT/samples/erd/simple-context/simple-context.mmd"

# 3. Class Diagram: Design Patterns
invoke_projgraph \
    "classdiagram ./samples/classdiagram/design-patterns/Domain/Order.cs --inheritance --dependencies --depth 2 --properties true --functions true" \
    "$ROOT/samples/classdiagram/design-patterns/design-patterns.mmd"

# 4. Class Diagram: Complex Hierarchy
invoke_projgraph \
    "classdiagram ./samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs --inheritance --dependencies --depth 5 --properties true --functions true" \
    "$ROOT/samples/classdiagram/complex-hierarchy/complex-hierarchy.mmd"

# 5. Class Diagram: Simple Hierarchy
invoke_projgraph \
    "classdiagram ./samples/classdiagram/simple-hierarchy/Models/Admin.cs --inheritance --dependencies --depth 2 --properties true --functions true" \
    "$ROOT/samples/classdiagram/simple-hierarchy/simple-hierarchy.mmd"

# 6. Project Graph: Modular Architecture
invoke_projgraph \
    "visualize ./samples/visualize/modular-architecture/ModularArchitecture.slnx --format mermaid" \
    "$ROOT/samples/visualize/modular-architecture/modular-architecture.mmd"

# 7. Project Graph: Simple Dependencies
invoke_projgraph \
    "visualize ./samples/visualize/simple-dependencies/simple-dependencies.slnx --format mermaid" \
    "$ROOT/samples/visualize/simple-dependencies/simple-dependencies.mmd"

echo -e "\n\033[0;32m--- All snapshots processed ---\033[0m"
