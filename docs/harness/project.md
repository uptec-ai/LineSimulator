# Project Harness Adapter

Agent-facing canonical file. Korean reader version: `project.ko.md`.

## Detected Project

- Root: `C:\Project\3. LineSimulator\TestMcAlgorithm`
- Solution: `TestMcAlgorithm.sln`
- Project: `TestMcAlgorithm.csproj`
- Output type: WPF `WinExe`
- Target framework: `net8.0-windows`
- Nullable: enabled
- Implicit usings: enabled

## Dependencies

- DevExpress WPF v24.2 through package and local DLL references.
- SciChart 8.11 through local NuGet package DLL references.
- NModbus 3.0.81.
- System.Text packages and System.Drawing.Common local reference.

## Build Command

```powershell
dotnet build .\TestMcAlgorithm.sln -c Debug
```

Use Release only when the user asks for release validation.

## Test Command

```powershell
dotnet test .\TestMcAlgorithm.sln --no-build
```

No dedicated test project was found during harness creation. This command is retained as a discovery gate.

## Static Analysis

No `.editorconfig`, analyzer config, or lint command was found during harness creation. The static analysis script is a placeholder that reports this and exits successfully.

## E2E

No automated WPF UI/E2E test harness was found. Manual validation in Visual Studio remains the expected UI validation path unless the project later adds an automated WPF test tool.

## Notes For Future Changes

- Algorithm code lives primarily in `Services/McAlgorithmService.cs` and related `Models/`.
- Modbus communication lives in `Services/ModbusTcpEndpointClient.cs` and `Services/ModbusTcpGatewayService.cs`.
- Main screen behavior is concentrated in `ViewModels/LineSimulatorViewModel.cs` and `MainWindow.xaml`.
- Avoid broad reshaping of XAML or ViewModel responsibilities unless refactoring is explicitly requested.
