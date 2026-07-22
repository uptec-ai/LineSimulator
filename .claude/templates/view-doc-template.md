<!-- COPY to .claude/rules/views/<name>.md; the paths block scopes when this loads. -->
---
paths:
  - "Views/<Name>.xaml"
  - "Views/<Name>.xaml.cs"
  - "ViewModels/<Name>ViewModel.cs"
---
# View: <Name>

## Purpose
<what this screen/control does for the operator>

## Owner ViewModel
<ViewModels/<Name>ViewModel.cs — key state & commands>

## Data & external I/O
<Modbus tags/registers, services consumed, settings models>

## UI surface
<notable controls, DevExpress/SciChart usage, dialogs raised>

## Gotchas / rules
<close pattern, threading, endianness, anything easy to break>

## Related
<.claude/rules/*.md, .claude/docs/*, Document/* specs>
