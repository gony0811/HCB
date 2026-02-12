# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

HCB (Hybrid Chip Bonding Machine) — an industrial equipment control application for semiconductor hybrid bonding. WPF-based HMI with motion control (Power PMAC), I/O management, recipe handling, and automated sequences.

## Build & Run Commands

```bash
# Restore packages (requires Telerik NuGet credentials in env vars TELERIK_USERNAME, TELERIK_PASSWORD)
dotnet restore src/HCB.sln

# Build
dotnet build src/HCB.sln
dotnet build src/HCB.sln --configuration Release

# Run (simulation mode by default via appsettings.json "Simulation": true)
dotnet run --project src/HCB.UI
```

**EF Core Migrations** (run from repo root):
```bash
dotnet ef migrations add <MigrationName> --project src/HCB
```
Database auto-migrates and seeds on application startup — no manual `dotnet ef database update` needed.

## Architecture

Two-project solution (`src/HCB.sln`):

- **HCB** (`src/HCB/`) — Core/data layer: EF Core entities, repositories, migrations, IoC attributes
- **HCB.UI** (`src/HCB.UI/`) — WPF application: views, viewmodels, services, device drivers, sequences

### Dependency Injection (Autofac, convention-based)

Custom attributes in `HCB/IoC/Attribute.cs` drive auto-registration:
- `[Service(Lifetime)]` — business logic services
- `[Repository(Lifetime)]` — data repositories
- `[ViewModel(Lifetime)]` — viewmodels
- `[View(Lifetime)]` — views/user controls

Registration scans assemblies in `HCB.UI/StartUp.cs` → `ContainerExtensions.RegisterByConvention()`. Views auto-bind to ViewModels by naming convention (`FooView` → `FooViewModel`).

### MVVM (CommunityToolkit.Mvvm)

ViewModels extend `ObservableObject`. Use `[ObservableProperty]` for bindable fields and `[RelayCommand]` for commands (source-generated).

### Startup Flow

`App.OnStartup()` → mutex single-instance check → splash screen → `StartUp.BuildHost()` (Serilog, Autofac, EF Core) → `InitDatabaseAsync()` (migrations + seed) → `RecipeService.Initialize()` → `UserService.InitializeAsync()` → device connections (if operation mode) → show `UMain`.

Entry points: `src/HCB.UI/App.xaml.cs` and `src/HCB.UI/StartUp.cs`.

### Device Layer (`src/HCB.UI/DEVICE/`)

Interfaces: `IDevice`, `IMotionDevice`, `IIoDevice`, `IAxis`, `IIoData`

Implementations:
- `PowerPmacDevice` — motion controller via native DLLs (`PowerPmac32/64.dll`)
- `PmacIoDevice` — I/O device
- `DeviceManager` orchestrates all devices; `SystemMainService` polls at 100ms

Factory pattern: `DeviceFactory`, `MotionFactory`, `IoDataFactory` create device instances from DB entities.

### Sequence Layer (`src/HCB.UI/SERVICE/Sequence/`)

Automated equipment sequences: `MainSequence`, `InitSequence`, `BondingSequence`, `WaferSequence`, `DieSequence`, `StepSequence`, `PTableSequence`, `ManualSequence`. Orchestrated by `SequenceService` (IHostedService).

### Data Layer (`src/HCB/Data/`)

SQLite via EF Core. `AppDb` DbContext. Repository pattern (`DbRepository<T>` base). Key entities: Device, Recipe/RecipeParam, Alarm/AlarmHistory, MotionEntity/MotionParameter/MotionPosition, IoDataEntity, Role/Screen/RoleScreenAccess.

Database file: `Data/db/hcb.db` (gitignored, auto-created on first run).

### UI Structure (`src/HCB.UI/MAIN UI/`)

8 tab screens: USub01 (Main — Auto/Manual/Loading/StepSeq), USub02 (Parameter), USub03 (User), USub04 (Log), USub05 (Alarm), USub06 (Motion), USub07 (I/O), USub08 (Device).

Reusable components in `SUB UI/`: MotionMoveController, PositionTable, MotorStatusTable, WaferMapControl, StateCell, numeric/password pads.

## Key Dependencies

- **Telerik UI for WPF** (v2025.4) — primary UI component suite (RadGridView, RadWindow, Windows11 theme). NuGet source configured in `src/nuget.config`.
- **CommunityToolkit.Mvvm** — source-generated MVVM
- **Autofac** — DI container
- **EF Core 9 + SQLite** — data persistence
- **Serilog** — structured logging (file, debug, custom UI sinks)
- **System.Reactive** — device status streaming
- **Power PMAC native DLLs** — motion controller communication

## Conventions

- Views and ViewModels are matched by name: `USub01` view ↔ `USub01ViewModel`
- Folder structure uses UPPERCASE for major layers: `MAIN UI/`, `SUB UI/`, `DEVICE/`, `SERVICE/`, `SYSTEM/`
- Logging: inject `ILogger`, call `logger.ForContext<T>()` for class-scoped context
- Configuration in `src/HCB.UI/appsettings.json` — `Data.Simulation` flag controls hardware simulation mode
