# LineSimulator 에이전트 가이드

에이전트가 직접 읽는 기준 문서는 `AGENTS.md`입니다. 이 파일은 사용자가 읽기 위한 한국어 버전입니다.

## 워크스페이스 경계

- 프로젝트 루트는 `C:\Project\3. LineSimulator\TestMcAlgorithm`로 봅니다.
- 사용자가 명시적으로 승인하지 않는 한 `C:\Project\3. LineSimulator` 밖의 파일은 수정, 저장, 삭제하지 않습니다.
- Harness 상태는 `.harness/`, 문서는 `docs/harness/`, 자동화 스크립트는 `scripts/harness/` 아래에 둡니다.

## 개발 환경

- IDE: Visual Studio 2022.
- 앱 유형: WPF 데스크톱 애플리케이션.
- Framework: .NET 8, `net8.0-windows`.
- UI 라이브러리: DevExpress v24.2, SciChart.
- 현재 솔루션: `TestMcAlgorithm.sln`.
- 현재 앱 프로젝트: `TestMcAlgorithm.csproj`.

## 프로젝트 구조

- `Models/`: 상태, 정의, 알고리즘 결과 record, 엔드포인트 설정 모델.
- `Services/`: 알고리즘 및 Modbus 통신 서비스.
- `ViewModels/`: MVVM 상태와 command 로직.
- `Views/`: WPF window와 control.
- `Assets/`, `Fonts/`, `ThemedSplashScreen/`: WPF 리소스.

## 코드 스타일

- 기존 MVVM 구조를 유지합니다.
- C# 파일은 file-scoped namespace 스타일을 유지합니다.
- nullable reference type이 켜져 있으므로 불필요한 suppression을 피합니다.
- ViewModel의 변경 가능한 property는 기존 `ObservableObject.SetProperty` 패턴을 우선합니다.
- command는 기존 `RelayCommand`, `AsyncRelayCommand` 패턴을 우선합니다.
- 가능하면 service 로직은 XAML code-behind 밖에 둡니다.
- 사용자가 명시적으로 요청하지 않으면 리팩토링하지 않습니다.

## 빌드와 테스트

- 기본 빌드 게이트: `dotnet build TestMcAlgorithm.sln -c Debug`.
- 현재 저장소에는 전용 테스트 프로젝트가 없습니다.
- `dotnet test TestMcAlgorithm.sln --no-build`는 테스트 탐지용 게이트로 사용할 수 있으며 테스트 없음으로 끝날 수 있습니다.
- WPF, DevExpress, SciChart 참조는 로컬 PC 환경에 의존합니다. vendor DLL 누락으로 빌드가 실패하면 프로젝트 참조를 바꾸지 말고 환경 문제로 보고합니다.

## Harness

- 애플리케이션 코드를 수정하기 전에 이 파일과 `docs/harness/workflow.md`를 읽습니다.
- `scripts/harness/start-task.ps1`로 작업을 시작합니다.
- 애플리케이션 코드를 수정하기 전에 생성된 영어 plan 파일을 채웁니다.
- `scripts/harness/guard-before-edit.ps1`가 통과한 뒤에만 진행합니다.
- 중요한 결정은 `scripts/harness/write-log.ps1`로 기록합니다.
- `scripts/harness/run-quality-gates.ps1`로 검증합니다.
- `scripts/harness/complete-task.ps1`로 작업을 마무리합니다.
