---
paths:
  - "Views/BusDiagramControl.xaml"
  - "Views/BusDiagramControl.xaml.cs"
  - "ViewModels/BusDiagramViewModel.cs"
---
# View: BusDiagramControl

## Purpose
Visualizes BUS1/BUS2/BUS3 layout, feeders, and MC/output states; entry point to
device detail.

## Owner ViewModel
`ViewModels/BusDiagramViewModel.cs` — VM class name is **`BusDiagram`** (code-behind
checks `DataContext is BusDiagram`).

## Data & external I/O
Feeder/output/device state from the simulator VM; live MC + bus-output status
(colored via `Converters/BusOutputStatusToBrushConverter.cs`).

## UI surface
Interactive feeder/output/device elements keyed by `FrameworkElement.Tag`
(feeder label / output title / device key); clicking a device opens
`DeviceDetailWindow`.

## Gotchas / rules
- Code-behind reads `Tag` strings — keep XAML `Tag` values in sync with the VM's keys.
- Status→brush mapping lives in the converter; don't inline color logic in code-behind.

## Related
`.claude/rules/mvvm.md`, `.claude/rules/views/device-detail-window.md`,
`.claude/rules/algorithm.md`.
