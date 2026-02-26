<#
.SYNOPSIS
    Regenerates all Mermaid snapshots for the samples showcase.
    This script ensures that the documentation is always up-to-date with the latest ProjGraph CLI output.

.DESCRIPTION
    The script iterates through the canonical sample commands defined in the documentation
    and generates Mermaid diagrams (.mmd) into each sample's 'snapshots/' directory.

.EXAMPLE
    .\regenerate-samples.ps1
#>

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$cliProject = Join-Path $root "src\ProjGraph.Cli\ProjGraph.Cli.csproj"

function Invoke-ProjGraph {
    param(
        [string]$Arguments,
        [string]$OutputPath
    )
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host "Rendering: $OutputPath" -ForegroundColor Cyan
    
    # Run the CLI tool
    # Paths in $Arguments are relative to the repository root
    Push-Location $root
    try {
        $allArgs = @("run", "--project", "$cliProject", "--no-build", "--") + $Arguments.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)
        & dotnet $allArgs
    }
    finally {
        Pop-Location
    }
    
    $stopwatch.Stop()
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully generated snapshot in $($stopwatch.Elapsed.TotalSeconds.ToString("F2"))s: $(Split-Path $OutputPath -Leaf)" -ForegroundColor Green
    } else {
        Write-Error "Failed to generate snapshot for: $Arguments"
    }
}

Write-Host "--- ProjGraph Samples Regeneration ---" -ForegroundColor White -BackgroundColor Blue

# Ensure the CLI is built first
Write-Host "Building CLI tool..." -ForegroundColor Gray
dotnet build "$cliProject" -v q /nologo
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to build CLI project."; exit 1 }

# 1. ERD: Complex E-commerce
Invoke-ProjGraph `
    -Arguments "erd ./samples/erd/complex-ecommerce/Data/MyDbContext.cs --output ./samples/erd/complex-ecommerce/complex-ecommerce.mmd" `
    -OutputPath "./samples/erd/complex-ecommerce/complex-ecommerce.mmd"

# 2. ERD: Simple Context
Invoke-ProjGraph `
    -Arguments "erd ./samples/erd/simple-context/EntityFramework/MyDbContext.cs --output ./samples/erd/simple-context/simple-context.mmd" `
    -OutputPath "./samples/erd/simple-context/simple-context.mmd"

# 3. Class Diagram: Design Patterns
Invoke-ProjGraph `
    -Arguments "classdiagram ./samples/classdiagram/design-patterns/Domain/Order.cs --inheritance --dependencies --depth 2 --properties true --functions true --output ./samples/classdiagram/design-patterns/design-patterns.mmd" `
    -OutputPath "./samples/classdiagram/design-patterns/design-patterns.mmd"

# 4. Class Diagram: Complex Hierarchy
Invoke-ProjGraph `
    -Arguments "classdiagram ./samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs --inheritance --dependencies --depth 5 --properties true --functions true --output ./samples/classdiagram/complex-hierarchy/complex-hierarchy.mmd" `
    -OutputPath "./samples/classdiagram/complex-hierarchy/complex-hierarchy.mmd"

# 5. Class Diagram: Simple Hierarchy
Invoke-ProjGraph `
    -Arguments "classdiagram ./samples/classdiagram/simple-hierarchy/Models/Admin.cs --inheritance --dependencies --depth 2 --properties true --functions true --output ./samples/classdiagram/simple-hierarchy/simple-hierarchy.mmd" `
    -OutputPath "./samples/classdiagram/simple-hierarchy/simple-hierarchy.mmd"

# 5b. Class Diagram: Simple Hierarchy (Directory Scan)
Invoke-ProjGraph `
    -Arguments "classdiagram ./samples/classdiagram/simple-hierarchy/Models/ --inheritance --dependencies --properties true --functions true --output ./samples/classdiagram/simple-hierarchy/simple-hierarchy-folder.mmd" `
    -OutputPath "./samples/classdiagram/simple-hierarchy/simple-hierarchy-folder.mmd"

# 6. Project Graph: Modular Architecture
Invoke-ProjGraph `
    -Arguments "visualize ./samples/visualize/modular-architecture/ModularArchitecture.slnx --format mermaid --output ./samples/visualize/modular-architecture/modular-architecture.mmd" `
    -OutputPath "./samples/visualize/modular-architecture/modular-architecture.mmd"

# 7. Project Graph: Simple Dependencies
Invoke-ProjGraph `
    -Arguments "visualize ./samples/visualize/simple-dependencies/simple-dependencies.slnx --format mermaid --output ./samples/visualize/simple-dependencies/simple-dependencies.mmd" `
    -OutputPath "./samples/visualize/simple-dependencies/simple-dependencies.mmd"

# 8. Stats: Modular Architecture
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Write-Host "Running: stats (Modular Architecture)" -ForegroundColor Cyan
Push-Location $root
try {
    & dotnet run --project "$cliProject" --no-build -- stats ./samples/visualize/modular-architecture/ModularArchitecture.slnx
}
finally {
    Pop-Location
}
$stopwatch.Stop()
if ($LASTEXITCODE -eq 0) {
    Write-Host "Stats sample completed in $($stopwatch.Elapsed.TotalSeconds.ToString("F2"))s." -ForegroundColor Green
} else {
    Write-Error "Failed to run stats sample."
}

Write-Host "`n--- All snapshots processed ---" -ForegroundColor Green
