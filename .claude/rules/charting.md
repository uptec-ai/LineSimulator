---
paths:
  - "MainWindow.xaml"
  - "MainWindow.xaml.cs"
---
# SciChart charting (MainWindow)

SciChart **8.11.0.28985** is referenced via local NuGet DLLs and used in
`MainWindow.xaml`. It is the only charting surface.

- SciChart DLLs are machine-local (`HintPath` under `~/.nuget/packages/scichart/...`).
  A missing SciChart DLL is an **environment issue**, not a code fix.
- A SciChart runtime license is typically required; if charts render a watermark or
  throw a license error, that is environment/licensing — report it, don't disable the chart.
- Keep chart data-binding in the VM; don't push series/manipulation logic into code-behind.
