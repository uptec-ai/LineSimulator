# LineSimulator (TestMcAlgorithm) — Project Instructions

WPF desktop app that simulates power **line/bus conditions**: it computes a target
grid impedance, picks which magnetic contactors (MC1–MC10) to close for
BUS1/BUS2/BUS3, and drives real hardware over Modbus TCP. Operated from Visual
Studio 2022 on Windows.

Canonical agent rules live in **`@AGENTS.md`** (imported below). This file adds a
portable-root + token-scoped context layer *on top of* the existing PowerShell
harness (`docs/harness/`, `scripts/harness/`, `.harness/`) — it does not replace it.

@AGENTS.md

## Tech stack (source of truth = TestMcAlgorithm.csproj)
- .NET 8 WPF, `net8.0-windows`, `WinExe`; Nullable + ImplicitUsings enabled; MVVM.
- DevExpress WPF **24.2.14** (`DevExpress.Wpf.Core` pkg + local v24.2 DLLs).
- SciChart **8.11.0.28985** (local NuGet DLLs; used in `MainWindow.xaml`).
- NModbus **3.0.81** (Modbus TCP).
- System.Text.Json **10.0.5**.
- ⚠️ DevExpress / SciChart / System.Drawing use **machine-specific HintPaths**. A
  missing vendor DLL is an **environment issue** — report it, do not rewrite the
  `<Reference>` HintPaths.

## Project root — portable, never hardcode paths
This repo may be cloned anywhere. Resolve the root from the `.sln` anchor:
```powershell
$root = & "$PSScriptRoot\scripts\Resolve-ProjectRoot.ps1"   # walks up to *.sln
```
Use `$root` in build/run commands instead of `C:\Project\3. LineSimulator\...`.

## Build / run (details: docs/harness/project.md)
```powershell
dotnet build .\TestMcAlgorithm.sln -c Debug      # primary gate
dotnet test  .\TestMcAlgorithm.sln --no-build    # discovery gate (no test project yet)
```
Release build only when asked. `bin/Release/*.zip` are **hand-made release
deliverables — never wipe `bin/` wholesale** (regenerable compiled output only).

## Layout
- `Models/` — state, MC/K definitions, algorithm result records, Modbus protocol +
  monitoring, endpoint settings.
- `Services/` — `McAlgorithmService` (core algorithm); Modbus
  (`ModbusTcpEndpointClient`, `ModbusTcpGatewayService`, `ModbusTcpMonitoringServer`,
  `IModbusGatewayService`); `LogStore`.
- `ViewModels/` — MVVM state + commands (`ObservableObject`, `RelayCommand`).
- `Views/` + `MainWindow.xaml` — WPF windows/controls.
- `Converters/`, `Assets/`, `Fonts/`, `ThemedSplashScreen/` — WPF resources.
- `docs/harness/`, `scripts/harness/`, `.harness/` — existing task-lifecycle harness
  (see `@AGENTS.md` § Harness).

## Conventions (full list in `@AGENTS.md` § Coding Style)
- File-scoped namespaces; preserve the MVVM structure; `SetProperty` for mutable VM
  properties; `RelayCommand` / `AsyncRelayCommand` for commands.
- Keep service logic out of XAML code-behind.
- **Do not refactor unless explicitly asked.**

## Path-scoped rules & per-View docs (the token lever)
`.claude/rules/` holds cross-cutting rules (`algorithm`, `modbus`, `mvvm`,
`charting`) and per-View docs under `.claude/rules/views/`. Each has a `paths:`
front-matter glob, so it loads **only** when you open a matching file. Keep the
matching doc accurate when you change a unit. New View → copy
`.claude/templates/view-doc-template.md`, set `paths:`, fill it in.

## Domain quick-facts (full model: `.claude/docs/` + `ALGORITHM.md`)
- Target impedance `Zgrid = 380² / (Srated × SCR)`; tolerance ±0.3 mΩ.
- BUS1 built from feeder families: A(1444)→MC1/2/3/5/6, B(825)→MC4/7, C(962)→MC8,
  D(577)→MC9.
- Shared MC pool `MC1,MC3,MC8,MC9,MC10`; BUS2 then BUS3 use the leftover;
  **BUS3 is allowed only if BUS2 exists.**
- `MC1–MC10` are algorithm-driven; `MC11–MC19` are manual reserve.
- "SCR 투입 실행" closes selected MCs low→high number at 1-second intervals.

## Worktree routing (always applies — not just for multi-task)
Three feature worktrees exist alongside this repo. **Any request that edits a file
matching the table below happens in that worktree folder — never edited directly in
the main repo** — whether it's a one-off tweak or a multi-task run. This keeps the
main working tree for shared files + harness config only.

| File area (glob) | Worktree folder (absolute path) | Branch |
| --- | --- | --- |
| `**/McAlgorithmService.cs`, `**/{AlgorithmPlan,McDefinition,KDefinition,BusRequestSpec}.cs`, `ALGORITHM.md` | `C:/Project/3. LineSimulator/TestMcAlgorithm-algorithm` | feature/algorithm |
| `**/ModbusTcp*.cs`, `**/IModbusGatewayService.cs`, `**/Modbus*Definitions.cs`, `**/ModbusMonitoringClientModels.cs`, `**/OvrEndpointSettingsModels.cs` | `C:/Project/3. LineSimulator/TestMcAlgorithm-modbus` | feature/modbus |
| `Views/**`, `ViewModels/**`, `Converters/**`, `**/MainWindow.xaml*` | `C:/Project/3. LineSimulator/TestMcAlgorithm-ui` | feature/ui |

Before editing a matching file, call `EnterWorktree({ path: "<absolute path above>" })`
to switch into that existing worktree (already registered in `git worktree list` — do
not create a new one). Use the **absolute path** verbatim. When done, call
`ExitWorktree({ action: "keep" })` — these are long-lived feature worktrees, so never
`"remove"` them. Only files NOT matching the table (shared files, `.claude/**` harness
config: `App.xaml*`, `AssemblyInfo.cs`, `Resources*`, `*.csproj`, `*.sln`, `docs/harness/**`,
`scripts/**`, `AGENTS*.md`, `CLAUDE.md`) are edited directly in this main repo folder.

## Boundaries
- Treat `TestMcAlgorithm/` as the project root; don't touch files outside
  `C:\Project\3. LineSimulator` without explicit approval.
- `.claude/**` docs are written in English (Korean mirrors already exist as
  `*.ko.md` / `docs/harness/*.ko.md`).
- Never edit build artifacts (`bin/`, `obj/`, `.vs/`).

## Agent team (personas in `.claude/agents/`)
Domain specialists, invoked via `agentType`: `algorithm-engineer` (MC selection /
impedance), `modbus-comms-engineer` (Modbus TCP), `wpf-ui-engineer` (WPF/MVVM/charts),
`build-quality-verifier` (gates & build). Orchestration/parallel execution is owned by
the `multi-task` workflow from `init-multi-task` — the single orchestrator. All agents
follow the existing PowerShell harness gates (`@AGENTS.md` § Harness).

**Change history**
| Date | Change | Target | Reason |
| --- | --- | --- | --- |
| 2026-07-22 | Initial context layer (CLAUDE.md, rules, docs, memory, root resolver) | harness | Layer on top of existing AGENTS.md harness |
| 2026-07-22 | Added 4 agent personas | `.claude/agents/*` | harness-team: domain specialist team |
