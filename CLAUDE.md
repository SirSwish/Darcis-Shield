# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Darcis Shield** is a WPF/.NET 8 modding toolkit for the 1999 game *Urban Chaos*. It consists of a launcher and five independent editors, each handling a specific game file format (`.iam` maps, `.lgt` lighting, `.ucm` missions, `.sty` storyboards, `.tma` styles).

## Build & Run

```bash
# Build entire solution
dotnet build "Darcis Shield.sln"

# Build a specific project
dotnet build "Urban Chaos Map Editor/Urban Chaos Map Editor.csproj"

# Run a specific editor directly
dotnet run --project "Urban Chaos Map Editor/Urban Chaos Map Editor.csproj"

# Run the launcher (entry point)
dotnet run --project "DarciShield.Launcher/DarciShield.Launcher.csproj"
```

There are no automated tests in this repository.

## Architecture

### Multi-Application Structure

The solution is a launcher + five independent WPF apps that share a common library:

```
DarciShield.Launcher           → launches individual editor executables via Process.Start()
Urban Chaos Map Editor         → .iam map files
Urban Chaos Light Editor       → .lgt lighting files
Urban Chaos Mission Editor     → .ucm mission files
Urban Chaos Storyboard Editor  → .sty campaign/storyboard files
Urban Chaos Style Editor       → .tma texture/style files
UrbanChaosEditor.Shared        → common models, services, base classes, assets
```

The Mission Editor is the most complex — it references the Light Editor, Map Editor, Storyboard Editor, and Shared projects.

### Service Layer (Singleton Pattern)

All data management goes through lazy-initialized singletons accessed via `.Instance`:

- `MapDataService.Instance` — map file I/O and in-memory state
- `TextureCacheService.Instance` — texture preloading and caching
- `LightsDataService.Instance` — lighting data
- `StyleDataService.Instance` — style/texture mapping
- `RecentFilesService.Instance` — MRU file tracking

Services communicate via events (`MapLoaded`, `MapCleared`, `MapSaved`, `DirtyStateChanged`, `MapBytesReset`). The pattern is: service raises event → UI subscribes and updates.

### MVVM Pattern

Each editor follows standard MVVM:
- **Models** — binary game file data structures (e.g. `Prim`, `BuildingData`, `WallSegment`)
- **ViewModels** — extend `BaseViewModel : INotifyPropertyChanged`, use custom `RelayCommand`
- **Views** — XAML with data binding; canvas-based rendering for map visualization

The Map Editor's `EditorTool` enum in `Models/Core/EditorTool.cs` drives which tool is active (paint, place prim, etc.).

### Binary File Formats

Game files are binary. Each editor's service layer handles parsing and serialization. When editing file format code, the binary layout must match the game's exact byte structure.

### Asset References

Texture and prim graphic assets are embedded in the Map Editor project and linked via `<Link>` references in other editors' `.csproj` files. Asset URIs use `pack://application:///` format.

### Shared UI Conventions

- Dark title bar enabled via DWM attributes (Windows 10/11)
- Tab-based layout in editors — each major feature area is a separate `UserControl` tab
- Zoom/pan canvas controls with mouse and keyboard input
