---
paths:
  - "Views/IpSettingsWindow.xaml"
  - "Views/IpSettingsWindow.xaml.cs"
  - "ViewModels/IpSettingsWindowViewModel.cs"
---
# View: IpSettingsWindow

## Purpose
Edits Modbus TCP endpoint/IP connection settings.

## Owner ViewModel
`ViewModels/IpSettingsWindowViewModel.cs`.

## Data & external I/O
Endpoint settings models (`Models/OvrEndpointSettingsModels.cs`) feeding the
Modbus gateway/endpoint clients.

## UI surface
Settings dialog; closes via VM `CloseRequested`.

## Gotchas / rules
- Wire/unwire `CloseRequested` on `DataContextChanged` (both old and new VM).
- Changing endpoint defaults affects live hardware connections — confirm intent.

## Related
`.claude/rules/modbus.md`, `.claude/rules/mvvm.md`.
