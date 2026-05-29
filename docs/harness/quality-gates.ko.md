# Quality Gates

에이전트가 직접 읽는 기준 문서는 `quality-gates.md`입니다. 이 파일은 사용자가 읽기 위한 한국어 버전입니다.

## 게이트 순서

1. `run-unit-tests.ps1`
2. `run-static-analysis.ps1`
3. `run-build.ps1`
4. `run-e2e.ps1`

`run-quality-gates.ps1`는 첫 실패 지점에서 중단합니다.

## 현재 기대값

- Unit tests: 현재 테스트 프로젝트가 없으므로 탐지 상태를 보고합니다.
- Static analysis: 현재 analyzer 명령이 없으므로 탐지 상태를 보고합니다.
- Build: 필요한 DevExpress와 SciChart 참조가 있는 PC에서는 Debug build가 통과해야 합니다.
- E2E: 자동 E2E가 없으므로 수동 WPF 검증 필요 상태를 보고합니다.

## 환경 실패

빌드 출력에서 DevExpress, SciChart, WindowsDesktop 로컬 DLL 누락이 보이면 환경 실패로 취급합니다. 사용자가 dependency 정리를 요청하지 않는 한 프로젝트 참조를 바꾸지 않습니다.
