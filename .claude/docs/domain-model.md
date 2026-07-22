# Domain model — LineSimulator (선로모의장치)

On-demand reference. Authoritative source: `ALGORITHM.md` (root). This is an
English distillation for quick orientation; when they disagree, `ALGORITHM.md` wins.

## What the device does
A **line simulator** reproduces a target grid short-circuit impedance so equipment
can be tested against realistic bus conditions. Software selects and energizes
magnetic contactors (MCs) that switch RLC feeder banks onto BUS1/BUS2/BUS3.

## Core computation
- **Target impedance:** `Zgrid = 380² / (Srated × SCR)` (line-to-line 380 V).
  `Srated` = rated power, `SCR` = short-circuit ratio input by the operator.
- **BUS1** is assembled from feeder *families* whose per-family line values are:
  - `A = 1444` → contactors MC1, MC2, MC3, MC5, MC6
  - `B = 825`  → MC4, MC7
  - `C = 962`  → MC8
  - `D = 577`  → MC9
- All A/B/C/D **count** combinations are tried; a candidate is valid when it lands
  within **±0.3 mΩ** of `Zgrid`.

## MC assignment (once family counts are fixed)
1. BUS1 priority — 1st: `MC2, MC4, MC5, MC6, MC7`; 2nd (shared): `MC1, MC3, MC8, MC9`.
   `A` family fills in order `MC2 → MC5 → MC6 → MC1 → MC3`.
2. **Shared pool** = `MC1, MC3, MC8, MC9, MC10` minus whatever BUS1 already used.
3. From the leftover shared pool, solve **BUS2**, then **BUS3**.
4. **BUS3 is allowed only when BUS2 exists.**

## MC ranges
- `MC1–MC10` — algorithm-driven (this app selects them).
- `MC11–MC19` — manual reserve (operator-only, not part of the auto algorithm).

## Execution ("SCR 투입 실행")
Selected MCs are energized in ascending number order (low → high), **1 second apart**.

## Where it lives in code
- Algorithm: `Services/McAlgorithmService.cs` (+ `Models/AlgorithmPlan.cs`,
  `McDefinition.cs`, `KDefinition.cs`, `BusRequestSpec.cs`).
- Hardware I/O: Modbus TCP services in `Services/` (see `.claude/rules/modbus.md`).
- Operator UI: `MainWindow.xaml` + `LineSimulatorViewModel` (see `.claude/rules/views/main-window.md`).
