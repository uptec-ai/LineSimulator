# Quality Gates

Agent-facing canonical file. Korean reader version: `quality-gates.ko.md`.

## Gate Order

1. `run-unit-tests.ps1`
2. `run-static-analysis.ps1`
3. `run-build.ps1`
4. `run-e2e.ps1`

`run-quality-gates.ps1` stops on the first failing gate.

## Current Expectations

- Unit tests: no test project currently exists, so the gate reports discovery status.
- Static analysis: no configured analyzer command currently exists, so the gate reports discovery status.
- Build: Debug build must pass on a machine with the required DevExpress and SciChart references.
- E2E: no automated E2E exists, so the gate reports that manual WPF validation is required.

## Environment Failures

If build output reports missing local vendor DLLs under DevExpress, SciChart, or WindowsDesktop reference paths, treat the result as an environment failure. Do not rewrite project references unless the user asks for dependency cleanup.
