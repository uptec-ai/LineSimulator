# .claude/docs — on-demand reference index

These are **read on demand** (not auto-loaded). Open the file you need.

| Doc | What it covers |
| --- | --- |
| `domain-model.md` | Distilled MC-selection / bus / SCR domain model (English). |

## Cited source-of-truth files (not duplicated here)
- **Algorithm:** `ALGORITHM.md` (repo root, Korean, authoritative).
- **Existing harness:** `AGENTS.md`, `docs/harness/{project,workflow,quality-gates}.md`
  (+ `*.ko.md`), `scripts/harness/*.ps1`.
- **Modbus protocol:** `Document/Protocol/LineSimulator_Modbus_Protocol.xlsx`,
  code in `Models/ModbusProtocolDefinitions.cs`.
- **Device register maps** (`Document/RLC부하장치_..._통신 프로토콜 자료/통신자료/`):
  EOCR-iSEM2 (`EOCR-iSEM2_RegisterMap_*.xlsx`), GIMAC1000 (`[GIMAC1000] Modbus Map_*.pdf`),
  ZH194F power meter, Schneider EOCR ISEM2 manuals.
- **Circuit / model:** `Document/선로모의장치 회로도.pdf`, `Document/circuit_diagram-Model(3P4W).pdf`,
  `Document/BUS-Model_Rev10.pdf`, the `회로도1P_분석_*.xlsx` condition/combination tables.
- **Tags / logging:** `Document/태그.xlsx`, `Document/LOG LEVEL.xlsx`.

> The `Document/` folder lives in the **parent** `3. LineSimulator/` directory,
> outside the git repo. Treat those files as read-only reference.
