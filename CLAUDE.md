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

## Bonding Alignment Pipeline

전체 흐름: `TopHighAlign → BtmHighAlign → CoordinateSystemIntegration → BondingCorr → BondingPress`

`AlignData`가 전 과정의 공유 데이터 객체로, 각 단계가 측정/계산 결과를 누적 저장한다.

### TopHighAlign (DieSequence.cs)
Top Die 고배율 비전 측정. PC Table 카메라로 4개 마크 순차 촬상.
- `LoadCalibrationInto()` → DB에서 캘리브레이션 파라미터(Hcro, Hc2Offset, OffsetXY, OffsetT) 로드
- `PTable2DMappingOn()` → 2D 매핑 보정 활성화 (옵션)
- RightFid → RightAlign → LeftFid → LeftAlign 순서로 촬상
- 출력: `data.TopRightFidRaw`, `TopRightAlignRaw`, `TopLeftFidRaw`, `TopLeftAlignRaw` (CenterX/Y 절대 픽셀좌표)
- 옵션: `MeasureFiducialDrift()` — HC1/HC2 드리프트 측정

### BtmHighAlign (DieSequence.cs)
Btm Die 고배율 비전 측정. HC1/HC2 카메라 사용.
- `WTable2DMappingOn()` → Wafer Table 2D 매핑 전환 (옵션)
- `TopDieSet()` → Head를 본딩 위치(Z축 하강)로 이동
- HC1/HC2 피듀셜 위치 보정값 적용 (`hc1FidOffset`, `hc2FidOffset`)
- 개별 측정: RightFid → RightAlign → LeftFid → LeftAlign 순차, 동시 측정: `BtmDieVisionAlign()` 한 번에 4점
- 출력: `data.BtmRightFidRaw`, `BtmRightAlignRaw`, `BtmLeftFidRaw`, `BtmLeftAlignRaw` (DxCamToMark 카메라 중심 대비 상대거리)
- **좌표 차이**: Top은 PC카메라 CenterX/Y (절대), Btm은 HC카메라 DxCamToMark (상대)

### CoordinateSystemIntegration (MainSequence.cs)
Top/Btm 측정 좌표를 통합하여 보정량(ResultX, ResultY, ResultT) 계산.
1. **TracingMode 분기**: Auto → `CompensateHc2Offset()` (피듀셜 드리프트 기반 Hc2Offset/Hcro 보정), Manual → `CamDistAndHcro()` (카메라 거리 측정 + 3점 회전으로 FitCircle 회전중심), None → 스킵
2. **Theta 보정**: M2/M3 FidTheta 변화량 → Top 마크 4점 `RotateAroundPivot`
3. **Fid→Align 이동량**: `lDist = TopLeftAlign - TopLeftFid`, `rDist = TopRightAlign - TopRightFid`
4. **좌표 통합**: Btm Fid 부호 반전(Stage→DxCam), Top Align 위치 = bfl - lDist, Btm Align = (-Raw) or (camOffset - Raw)
5. **HCRO 기준 이동**: bl, br, tl, tr 모두 `- hcro`
6. **θ 계산**: `thetaF = SPEC_THETA - ToDegree(atan2(tr-tl) - atan2(br-bl))`
7. **회전 보정**: Top 마크에 `ApplyRotation(thetaF_rad)`
8. **Shift**: `ResultX = (tCenter - bCenter).X + OffsetXY.X`, `ResultY = ... + OffsetXY.Y`, `ResultT = thetaF + OffsetT`

### BondingCorr (StepSequence.cs)
CoordinateSystemIntegration 결과를 모션 축에 적용하여 본딩 위치 보정.
- XY/T 보정 이동 (병렬): `H_X: -ResultX`, `W_Y: -ResultY`, `H_T: +ResultT`
- Z축 하강: `H_Z = ShankToWaferOffset - TopDieThickness - BtmDieThickness - READY_POSITION`
- 후속: `BondingPress()`가 PMAC 가압 시퀀스 실행 (status=6 완료 폴링, Vacuum OFF)

## Conventions

- Views and ViewModels are matched by name: `USub01` view ↔ `USub01ViewModel`
- Folder structure uses UPPERCASE for major layers: `MAIN UI/`, `SUB UI/`, `DEVICE/`, `SERVICE/`, `SYSTEM/`
- Logging: inject `ILogger`, call `logger.ForContext<T>()` for class-scoped context
- Configuration in `src/HCB.UI/appsettings.json` — `Data.Simulation` flag controls hardware simulation mode
