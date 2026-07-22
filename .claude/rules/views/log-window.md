---
paths:
  - "Views/LogWindow.xaml"
  - "Views/LogWindow.xaml.cs"
  - "ViewModels/LogWindowViewModel.cs"
---
# View: LogWindow

## Purpose
Displays application/communication logs to the operator.

## Owner ViewModel
`ViewModels/LogWindowViewModel.cs` (code-behind checks `DataContext is LogWindowViewModel`).

## Data & external I/O
Log entries from `Services/LogStore.cs` (models in `Models/LogModels.cs`,
`Models/LogDefinitions.cs`). Log levels: see `Document/LOG LEVEL.xlsx`.

## UI surface
Log list/grid (DevExpress).

## Gotchas / rules
- Read from `LogStore`; don't spin up a second log sink.
- Keep UI-thread marshaling correct when appending live log entries.

## Related
`.claude/rules/mvvm.md`.
