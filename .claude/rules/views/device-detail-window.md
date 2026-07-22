---
paths:
  - "Views/DeviceDetailWindow.xaml"
  - "Views/DeviceDetailWindow.xaml.cs"
  - "ViewModels/DeviceDetailWindowViewModel.cs"
---
# View: DeviceDetailWindow

## Purpose
Shows a single device's detail/metering (opened from the bus diagram).

## Owner ViewModel
`ViewModels/DeviceDetailWindowViewModel.cs`.

## Data & external I/O
Per-device metering from Modbus monitoring (`ModbusTcpMonitoringServer` /
monitoring client models); read-only view of device values.

## UI surface
Detail window; closes via the VM `CloseRequested` event.

## Gotchas / rules
- Code-behind subscribes/unsubscribes `CloseRequested` on `DataContextChanged` — keep
  both sides wired to avoid leaks.
- Monitoring data is read-only; don't add write paths here.

## Related
`.claude/rules/modbus.md`, `.claude/rules/views/bus-diagram-control.md`.
