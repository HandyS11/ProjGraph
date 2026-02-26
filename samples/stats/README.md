# Sample: Solution Metrics

This sample demonstrates the **`projgraph stats`** command, which analyses a .NET solution and prints key
architectural metrics to the terminal — project counts by type, dependency depth statistics, cycle detection, and the
most-referenced (hotspot) projects.

## 🏙️ Target Solution

This sample runs `stats` against the **Modular Architecture** solution that is also used by the `visualize` samples.
It contains five projects across three layers, making it a good illustration of depth and in-degree metrics.

| Project            | Type    | Description                          |
|--------------------|---------|--------------------------------------|
| `App`              | Exe     | Entry point; depends on all layers.  |
| `Services`         | Library | Orchestration; depends on Infra+Core.|
| `Infrastructure`   | Library | Persistence; depends on Core.        |
| `Core`             | Library | Domain layer; no dependencies.       |
| `Shared`           | Library | Common utilities; no dependencies.   |

## 📊 Expected Output

Running the command below produces output similar to:

```none
──────────── ModularArchitecture ────────────
 Total projects                           5
   Libraries                              4
   Executables                            1
   Test projects                          0
   Other                                  0
 Average depth                          1.6
 Min depth                                0
 Max depth                                3
 Cycles detected                         No
 Analysis time                          ~ms

 Most-referenced projects:
    1. Core                          ← 3
    2. Infrastructure                ← 2
```

> [!NOTE]
> Timings vary per machine. All other values are deterministic for this solution.

## 🚀 Quick Start

Run the following command from the repository root:

```bash
projgraph stats ./samples/visualize/modular-architecture/ModularArchitecture.slnx
```

### Show More Hotspot Projects

```bash
projgraph stats ./samples/visualize/modular-architecture/ModularArchitecture.slnx --top 5
```

## 🛠️ Build and Test

The target solution can be built independently:

```bash
dotnet build ./samples/visualize/modular-architecture/ModularArchitecture.slnx
```
