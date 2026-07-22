---
paths:
  - "MainWindow.xaml"
  - "MainWindow.xaml.cs"
  - "ViewModels/MainViewModel.cs"
  - "ViewModels/LineSimulatorViewModel.cs"
---
# View: MainWindow

## Purpose
Primary operator screen — drives the MC selection algorithm, SCR investment
execution, and the live SciChart display.

## Owner ViewModel
`ViewModels/MainViewModel.cs` (composition root) + `ViewModels/LineSimulatorViewModel.cs`
(main simulator state/commands). ViewModels were split intentionally ("ViewModel 분할").

## Data & external I/O
Modbus gateway via `IModbusGatewayService`; algorithm via `McAlgorithmService`;
SciChart series bound from the VM.

## UI surface
SciChart chart (only chart surface — see `.claude/rules/charting.md`); DevExpress
Office2019Colorful theme; hosts the bus diagram control.

## Gotchas / rules
- Keep algorithm + Modbus logic in services; MainWindow code-behind stays thin.
- Chart/vendor DLL failures are environment issues, not code fixes.

## Related
`.claude/rules/algorithm.md`, `.claude/rules/modbus.md`, `.claude/rules/charting.md`,
`ALGORITHM.md`.
