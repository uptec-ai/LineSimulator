# 프로젝트 Harness 어댑터

에이전트가 직접 읽는 기준 문서는 `project.md`입니다. 이 파일은 사용자가 읽기 위한 한국어 버전입니다.

## 감지된 프로젝트

- Root: `C:\Project\3. LineSimulator\TestMcAlgorithm`
- Solution: `TestMcAlgorithm.sln`
- Project: `TestMcAlgorithm.csproj`
- Output type: WPF `WinExe`
- Target framework: `net8.0-windows`
- Nullable: enabled
- Implicit usings: enabled

## 의존성

- DevExpress WPF v24.2: package 및 로컬 DLL 참조.
- SciChart 8.11: 로컬 NuGet package DLL 참조.
- NModbus 3.0.81.
- System.Text package들 및 System.Drawing.Common 로컬 참조.

## 빌드 명령

```powershell
dotnet build .\TestMcAlgorithm.sln -c Debug
```

Release 검증은 사용자가 요청할 때만 사용합니다.

## 테스트 명령

```powershell
dotnet test .\TestMcAlgorithm.sln --no-build
```

Harness 생성 시점에 전용 테스트 프로젝트는 발견되지 않았습니다. 이 명령은 테스트 탐지 게이트로 유지합니다.

## Static Analysis

Harness 생성 시점에 `.editorconfig`, analyzer config, lint 명령은 발견되지 않았습니다. Static analysis 스크립트는 이 상태를 보고하고 성공 처리하는 placeholder입니다.

## E2E

자동화된 WPF UI/E2E 테스트 Harness는 발견되지 않았습니다. 별도 자동화 도구가 추가되기 전까지 UI 검증은 Visual Studio에서 수동 확인하는 방식이 기본입니다.

## 향후 변경 참고

- 알고리즘 코드는 주로 `Services/McAlgorithmService.cs`와 관련 `Models/`에 있습니다.
- Modbus 통신은 `Services/ModbusTcpEndpointClient.cs`, `Services/ModbusTcpGatewayService.cs`에 있습니다.
- 메인 화면 동작은 `ViewModels/LineSimulatorViewModel.cs`, `MainWindow.xaml`에 집중되어 있습니다.
- 사용자가 리팩토링을 명시적으로 요청하지 않으면 XAML이나 ViewModel 책임을 크게 재구성하지 않습니다.
