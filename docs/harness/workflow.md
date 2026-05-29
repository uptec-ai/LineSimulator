# Harness Workflow

Agent-facing canonical file. Korean reader version: `workflow.ko.md`.

## Purpose

This harness gives every code-change task a repeatable path:

1. Start task.
2. Fill plan.
3. Guard before edit.
4. Implement and log decisions.
5. Run quality gates.
6. Suggest commit message.
7. Complete task.

## Task State

- Active task state is stored in `.harness/active-task.json`.
- Per-task artifacts are stored under `.harness/tasks/<task-id>/`.
- The agent-facing plan is `plan.en.md`.
- The Korean reader plan is `plan.ko.md`.
- The agent-facing log is `log.en.md`.
- The Korean reader log is `log.ko.md`.
- Completion summary is written to `summary.en.md` and `summary.ko.md`.

## Start Task

Run:

```powershell
.\scripts\harness\start-task.ps1 -Title "short task title"
```

The script creates a task directory, paired English and Korean plan/log files, and updates `.harness/active-task.json`.

## Fill Plan

Before editing application code, update `plan.en.md` at minimum:

- Goal
- Scope
- Out Of Scope
- Impacted Files
- Test Strategy
- Rollback

Keep `plan.ko.md` useful for the user when the task is large enough to warrant it. The guard checks the English plan only.

## Guard Before Edit

Run:

```powershell
.\scripts\harness\guard-before-edit.ps1
```

The guard verifies:

- The current directory is inside the project root.
- `AGENTS.md` and `docs/harness/workflow.md` exist.
- An active task exists.
- The English plan has been filled.
- The Git worktree state has been recorded.

If the guard fails, fix harness state before editing application code.

## Write Log

Run:

```powershell
.\scripts\harness\write-log.ps1 -Message "decision or observation"
```

Use the log for implementation decisions, risks, test findings, and environment issues.

## Quality Gates

Run:

```powershell
.\scripts\harness\run-quality-gates.ps1
```

Default order:

1. Unit test discovery gate.
2. Static analysis placeholder gate.
3. Debug build gate.
4. E2E placeholder gate.

This project currently has no dedicated test project or automated E2E suite. Placeholder gates report that status and pass so the harness can still provide a consistent workflow.

## Complete Task

Run:

```powershell
.\scripts\harness\complete-task.ps1
```

The script writes paired summaries, records current Git status when available, and clears the active task unless `-KeepActive` is supplied.
