---
paths:
  - "ViewModels/**/*.cs"
  - "Views/**/*.xaml.cs"
  - "MainWindow.xaml.cs"
  - "Converters/**/*.cs"
---
# MVVM conventions

- Mutable VM properties use `ObservableObject.SetProperty` (see `ViewModels/ObservableObject.cs`).
- Commands use `RelayCommand` / `AsyncRelayCommand` (`ViewModels/RelayCommand.cs`) —
  don't introduce a different command type.
- File-scoped namespaces; nullable reference types stay enabled — avoid `#nullable`
  suppressions without a clear reason.
- **Code-behind stays thin**: view logic reacts to `DataContext` (see the
  `DataContextChanged` pattern in `Views/*Window.xaml.cs`) and forwards to the VM.
  Service/algorithm logic belongs in `Services/`, not code-behind.
- Windows that self-close raise a VM event (`CloseRequested`) the code-behind
  subscribes to — follow that pattern rather than calling `Close()` from the VM.
- Don't refactor VM responsibilities or split/merge ViewModels unless explicitly asked.
