---
paths:
  - "Services/McAlgorithmService.cs"
  - "Models/AlgorithmPlan.cs"
  - "Models/McDefinition.cs"
  - "Models/KDefinition.cs"
  - "Models/BusRequestSpec.cs"
  - "ALGORITHM.md"
---
# MC assignment algorithm

Canonical spec: **`ALGORITHM.md`** (Korean, authoritative). Do not hardcode the
combination table — the flow computes it: *count feeder families → assign real MCs
→ fill BUS2/BUS3 from the shared pool*.

- `Zgrid = 380² / (Srated × SCR)`; a BUS1 candidate must land within **±0.3 mΩ**.
- BUS1 feeder families: `A=1444`, `B=825`, `C=962`, `D=577`. Try all A/B/C/D counts.
- BUS1 MC-assignment priority: 1st `MC2,MC4,MC5,MC6,MC7`; 2nd shared
  `MC1,MC3,MC8,MC9`. `A` family consumes MCs in order `MC2→MC5→MC6→MC1→MC3`.
- Shared pool = `MC1,MC3,MC8,MC9,MC10` minus MCs BUS1 already used → BUS2, then BUS3.
- **BUS3 requires BUS2.** `MC1–MC10` are algorithm-driven; `MC11–MC19` are manual reserve.
- "SCR 투입 실행" energizes selected MCs low→high number, **1 s apart**.

Rules:
- Changing family definitions, priorities, or tolerance changes hardware behavior —
  confirm intent and update `ALGORITHM.md` in the same change.
- Keep the algorithm in `McAlgorithmService`; don't leak selection logic into ViewModels/Views.
