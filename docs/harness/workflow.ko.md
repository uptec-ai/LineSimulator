# Harness 작업 흐름

에이전트가 직접 읽는 기준 문서는 `workflow.md`입니다. 이 파일은 사용자가 읽기 위한 한국어 버전입니다.

## 목적

이 Harness는 모든 코드 변경 작업을 같은 흐름으로 처리하기 위한 구조입니다.

1. 작업 시작.
2. 계획 작성.
3. 수정 전 guard 확인.
4. 구현 및 결정 기록.
5. quality gate 실행.
6. 커밋 메시지 제안.
7. 작업 완료.

## 작업 상태

- 활성 작업 상태는 `.harness/active-task.json`에 저장합니다.
- 작업별 산출물은 `.harness/tasks/<task-id>/` 아래에 저장합니다.
- 에이전트가 읽는 계획 파일은 `plan.en.md`입니다.
- 사용자가 읽는 한국어 계획 파일은 `plan.ko.md`입니다.
- 에이전트가 읽는 로그 파일은 `log.en.md`입니다.
- 사용자가 읽는 한국어 로그 파일은 `log.ko.md`입니다.
- 완료 요약은 `summary.en.md`, `summary.ko.md`로 작성합니다.

## 작업 시작

실행:

```powershell
.\scripts\harness\start-task.ps1 -Title "짧은 작업 제목"
```

스크립트는 작업 디렉터리, 영어/한국어 plan과 log 파일을 만들고 `.harness/active-task.json`을 갱신합니다.

## 계획 작성

애플리케이션 코드를 수정하기 전에 최소한 `plan.en.md`의 다음 항목을 채웁니다.

- Goal
- Scope
- Out Of Scope
- Impacted Files
- Test Strategy
- Rollback

작업 규모가 크면 `plan.ko.md`도 사용자가 이해할 수 있게 갱신합니다. Guard는 영어 plan만 검사합니다.

## 수정 전 Guard

실행:

```powershell
.\scripts\harness\guard-before-edit.ps1
```

Guard는 다음을 확인합니다.

- 현재 위치가 프로젝트 루트 안인지 확인합니다.
- `AGENTS.md`, `docs/harness/workflow.md`가 있는지 확인합니다.
- 활성 작업이 있는지 확인합니다.
- 영어 plan이 작성되어 있는지 확인합니다.
- Git worktree 상태가 기록되어 있는지 확인합니다.

Guard가 실패하면 애플리케이션 코드를 수정하기 전에 Harness 상태를 먼저 고칩니다.

## 로그 작성

실행:

```powershell
.\scripts\harness\write-log.ps1 -Message "결정 또는 관찰 내용"
```

구현 결정, 위험 요소, 테스트 결과, 환경 문제를 기록할 때 사용합니다.

## Quality Gates

실행:

```powershell
.\scripts\harness\run-quality-gates.ps1
```

기본 순서:

1. Unit test 탐지 게이트.
2. Static analysis placeholder 게이트.
3. Debug build 게이트.
4. E2E placeholder 게이트.

현재 프로젝트에는 전용 테스트 프로젝트와 자동 E2E suite가 없습니다. 해당 placeholder gate는 그 상태를 보고하고 통과하여 Harness 흐름을 유지합니다.

## 작업 완료

실행:

```powershell
.\scripts\harness\complete-task.ps1
```

스크립트는 영어/한국어 요약을 작성하고, 가능한 경우 현재 Git 상태를 기록한 뒤, `-KeepActive`가 없으면 활성 작업을 비웁니다.
