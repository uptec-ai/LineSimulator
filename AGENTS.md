# LineSimulator Agent Guide

Agent-facing canonical file. Korean reader version: `AGENTS.ko.md`.

## Workspace Boundary

- Treat `C:\Project\3. LineSimulator\TestMcAlgorithm` as the project root.
- Do not modify, save, or delete files outside `C:\Project\3. LineSimulator` unless the user explicitly approves it.
- Keep generated harness state under `.harness/`, documentation under `docs/harness/`, and automation under `scripts/harness/`.

## Environment

- IDE: Visual Studio 2022.
- App type: WPF desktop application.
- Framework: .NET 8, `net8.0-windows`.
- UI libraries: DevExpress v24.2 and SciChart.
- Current solution: `TestMcAlgorithm.sln`.
- Current app project: `TestMcAlgorithm.csproj`.

## Project Shape

- `Models/`: state, definitions, algorithm result records, endpoint settings models.
- `Services/`: algorithm and Modbus communication services.
- `ViewModels/`: MVVM state and command logic.
- `Views/`: WPF windows and controls.
- `Assets/`, `Fonts/`, `ThemedSplashScreen/`: WPF resources.

## Coding Style

- Preserve the existing MVVM structure.
- Use file-scoped namespaces for C# files.
- Keep nullable reference types enabled and avoid suppressions unless there is a clear reason.
- Prefer `ObservableObject.SetProperty` for mutable view model properties.
- Prefer existing `RelayCommand` and `AsyncRelayCommand` patterns for commands.
- Keep service logic outside XAML code-behind when practical.
- Do not refactor unless the user explicitly asks for refactoring.

## Build And Test

- Primary build gate: `dotnet build TestMcAlgorithm.sln -c Debug`.
- There is currently no dedicated test project in this repository.
- `dotnet test TestMcAlgorithm.sln --no-build` is allowed as a no-test discovery gate and may report that no tests are present.
- WPF, DevExpress, and SciChart references are local machine dependent. If a build fails because a referenced vendor DLL is missing, report that as an environment issue instead of changing project references.

## Harness

- Before application code edits, read this file and `docs/harness/workflow.md`.
- Start a task with `scripts/harness/start-task.ps1`.
- Fill the generated English plan file before editing application code.
- Run `scripts/harness/guard-before-edit.ps1` and proceed only after it passes.
- Log important decisions with `scripts/harness/write-log.ps1`.
- Validate with `scripts/harness/run-quality-gates.ps1`.
- Finish with `scripts/harness/complete-task.ps1`.
