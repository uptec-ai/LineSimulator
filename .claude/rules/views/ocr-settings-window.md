---
paths:
  - "Views/OcrSettingsWindow.xaml"
  - "Views/OcrSettingsWindow.xaml.cs"
  - "ViewModels/OcrSettingsWindowViewModel.cs"
---
# View: OcrSettingsWindow

## Purpose
Configures EOCR (electronic overcurrent relay) / OCR device settings and detail.

## Owner ViewModel
`ViewModels/OcrSettingsWindowViewModel.cs`.

## Data & external I/O
OCR device registers over Modbus (EOCR-iSEM2 register map in
`Document/RLC부하장치_.../통신자료/EOCR-iSEM2_RegisterMap_*.xlsx`).

## UI surface
Settings/detail dialog (added in the `[add] ocr settings view` / `ocr detail view` work).

## Gotchas / rules
- Register addresses come from the EOCR spec — verify against it before changing.
- Follow the `CloseRequested` close pattern.

## Related
`.claude/rules/modbus.md`, `.claude/docs/README.md`.
