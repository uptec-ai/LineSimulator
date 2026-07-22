---
name: multi-task
description: 작업 규모를 분석해 순차·병렬·팀토론 중 최적 전략을 자동 선택해 실행한다. 복수의 기능(algorithm·modbus·ui)을 동시에 개발하거나 복잡한 설계 결정이 필요할 때 사용한다.
---

## 도메인 특화 규칙

- 기능-Worktree 매핑:

  | 기능 | Worktree 경로 | Branch |
  |------|--------------|--------|
  | algorithm | `C:/Project/3. LineSimulator/TestMcAlgorithm-algorithm` | feature/algorithm |
  | modbus | `C:/Project/3. LineSimulator/TestMcAlgorithm-modbus` | feature/modbus |
  | ui | `C:/Project/3. LineSimulator/TestMcAlgorithm-ui` | feature/ui |

- 공유 파일 (메인 저장소 `TestMcAlgorithm/`에서만 수정): `App.xaml*`, `AssemblyInfo.cs`,
  `Resources*`, `*.csproj`, `*.sln`, `.claude/**`, `docs/harness/**`, `scripts/**`,
  `AGENTS*.md`, `CLAUDE.md`
- 통합 브랜치: `integrate/{작업명}-{YYYYMMDD}`
- 빌드 명령: `dotnet build TestMcAlgorithm.sln -c Debug`
- Agent Team: algorithm-engineer · modbus-comms-engineer · wpf-ui-engineer · build-quality-verifier
- Merge 순서: algorithm → modbus → ui (공유 파일은 메인에서 선반영)
- 모든 에이전트는 기존 PowerShell 하네스 게이트를 따른다 (`@AGENTS.md` § Harness):
  start-task → guard-before-edit → run-quality-gates → complete-task.

## 규모 분류 기준 (자동 선택)

| 규모    | 조건                                      | 자동 선택 전략                              |
|---------|-------------------------------------------|--------------------------------------------|
| small   | 단일 파일 · 공유 파일 미포함               | 순차 실행                                  |
| medium  | 복수 파일 · 독립 worktree 분리 가능        | 병렬 실행 + main 인라인 검토               |
| large   | 공유 파일 포함 · 아키텍처 변경 · 회귀 위험 | 병렬 실행 + integration-reviewer 에이전트  |
| complex | 설계 결정 필요 · 상충 요구사항             | 팀 토론 → 합의 → 병렬 실행 + reviewer     |

## 절차

1. 규모 분석 — 작업 목록을 분류 기준에 따라 분류하고 worktree에 배분한다.
2. 사용자 확인 — 분류 결과와 실행 전략을 보여주고 승인을 받는다.
3. 규모별 실행 (각 기능 작업은 해당 worktree 절대경로 안에서만 수정·빌드·커밋).
4. 통합 및 검토 (large/complex는 build-quality-verifier로 CRITICAL 확인).
5. 에러 핸들링 정책
   - **개별 작업 실패**: 담당 에이전트의 재시도 정책에 위임. 그래도 실패하면
     `buildSuccess: false`로 반환하고 워크플로우는 중단 없이 나머지를 계속 진행 —
     실패 작업은 최종 보고의 `failed` 목록에 오른다.
   - **에이전트 무응답** (스킵/터미널 오류): `agent()`가 `null` 반환 →
     `results.filter(Boolean)`로 제외. `succeeded`+`failed` 합이 요청 수보다 적으면
     최종 보고에서 누락을 확인한다.
   - **CRITICAL 검토 발견** (large/complex): 검토는 `findings[]` 구조로 응답하며,
     CRITICAL이 하나라도 있으면 `mergeBlocked: true` — true면 사용자 확인 없이
     integrate → main 병합을 진행하지 않는다.
   - **자동 재시도 없음**: 재시도 여부는 실패 목록을 본 사용자가 결정한다.
6. 최종 보고.

## 원칙

- 사용자 확인(2단계) 없이 실행을 시작하지 않는다.
- 기능 영역 파일은 규모·트리거 여부와 무관하게 항상 해당 worktree에서 수정한다
  (CLAUDE.md의 Worktree routing 규칙과 동일).
- small 티어도 worktree 격리·빌드·커밋을 거친다 — 다른 점은 순차 실행뿐이다.
- CRITICAL 검토 항목은 main merge 전에 반드시 해소한다 (`mergeBlocked` 확인).
- complex 작업은 합의 없이 구현을 시작하지 않는다.
- 벤더 DLL 부재(DevExpress/SciChart)는 환경 이슈로 보고 — `<Reference>` 경로를 고치지 않는다.

## 품질 체크리스트

- [ ] 규모 분류가 기준표와 일치하는가
- [ ] 사용자 확인을 거쳤는가
- [ ] complex 작업은 합의 단계를 거쳤는가
- [ ] 모든 feature-worker 결과를 수집했는가
- [ ] large/complex는 integration-reviewer(build-quality-verifier)를 실행했는가
- [ ] `succeeded`+`failed` 합이 요청 작업 수와 일치하는지 확인했는가
- [ ] `mergeBlocked`가 true이면 병합을 진행하지 않았는가

## 테스트 시나리오

- **정상 흐름 (medium):** "modbus 모니터링 서버에 하트비트 레지스터 추가, ui 상태표시등 추가"
  → 규모 medium 분류 → modbus/ui worktree 병렬 실행 → main 인라인 검토 → 보고.
- **에러 흐름 (large, CRITICAL):** "MC 배정 알고리즘 tolerance 변경 + 공유 csproj 참조 추가"
  → large 분류 → 병렬 실행 → build-quality-verifier가 하드웨어 계약 위반 CRITICAL 발견
  → `mergeBlocked: true` → 병합 중단, 사용자에게 보고.

## 산출물

파일 저장 원할 시: `multi-task-result-{YYYYMMDD}.md`
