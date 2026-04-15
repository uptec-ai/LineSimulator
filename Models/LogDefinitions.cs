namespace TestMcAlgorithm.Models;

public static class LogDefinitions
{
    public static readonly LogDefinition SystemAutoModeChanged = new("SYS-MODE-001", LogCategory.System, LogLevel.Info, "운전 모드 AUTO 전환", "운전 모드가 AUTO로 변경됨", "운전 모드가 AUTO로 변경되었습니다.");
    public static readonly LogDefinition SystemManualModeChanged = new("SYS-MODE-002", LogCategory.System, LogLevel.Info, "운전 모드 MANUAL 전환", "운전 모드가 MANUAL로 변경됨", "운전 모드가 MANUAL로 변경되었습니다.");
    public static readonly LogDefinition PlanCalculated = new("SYS-PLAN-001", LogCategory.System, LogLevel.Info, "자동 계산 완료", "용량/SCR 조합 계산 완료", "자동 계산이 완료되었습니다.");
    public static readonly LogDefinition PlanCalculationFailed = new("SYS-PLAN-002", LogCategory.System, LogLevel.Error, "자동 계산 실패", "용량/SCR 조합 계산 실패", "자동 계산에 실패했습니다.");
    public static readonly LogDefinition DeviceDetailRequested = new("SYS-DTL-001", LogCategory.System, LogLevel.Info, "상세 정보 조회", "OCR/PM 상세 정보 창 열기 요청", "장치 상세 정보 창을 열었습니다.");
    public static readonly LogDefinition DeviceDetailOpenFailed = new("SYS-DTL-002", LogCategory.System, LogLevel.Error, "상세 정보 조회 실패", "OCR/PM 상세 정보 창 열기 실패", "장치 상세 정보 창 열기에 실패했습니다.");
    public static readonly LogDefinition UserOperationCancelled = new("SYS-USER-001", LogCategory.System, LogLevel.Info, "사용자 동작 취소", "사용자가 수동 동작을 취소함", "사용자가 수동 동작을 취소했습니다.");

    public static readonly LogDefinition Bus1Applied = new("BUS-RUN-001", LogCategory.Operation, LogLevel.Info, "BUS OUT1 투입 완료", "BUS OUT1 투입 시퀀스 완료", "BUS OUT1 투입이 완료되었습니다.");
    public static readonly LogDefinition Bus2Applied = new("BUS-RUN-002", LogCategory.Operation, LogLevel.Info, "BUS OUT2 투입 완료", "BUS OUT2 투입 시퀀스 완료", "BUS OUT2 투입이 완료되었습니다.");
    public static readonly LogDefinition Bus3Applied = new("BUS-RUN-003", LogCategory.Operation, LogLevel.Info, "BUS OUT3 투입 완료", "BUS OUT3 투입 시퀀스 완료", "BUS OUT3 투입이 완료되었습니다.");
    public static readonly LogDefinition Bus1Stopped = new("BUS-STOP-001", LogCategory.Operation, LogLevel.Info, "BUS OUT1 정지 완료", "BUS OUT1 정지 시퀀스 완료", "BUS OUT1 정지가 완료되었습니다.");
    public static readonly LogDefinition Bus2Stopped = new("BUS-STOP-002", LogCategory.Operation, LogLevel.Info, "BUS OUT2 정지 완료", "BUS OUT2 정지 시퀀스 완료", "BUS OUT2 정지가 완료되었습니다.");
    public static readonly LogDefinition Bus3Stopped = new("BUS-STOP-003", LogCategory.Operation, LogLevel.Info, "BUS OUT3 정지 완료", "BUS OUT3 정지 시퀀스 완료", "BUS OUT3 정지가 완료되었습니다.");
    public static readonly LogDefinition BusApplyBlocked = new("BUS-RUN-101", LogCategory.Operation, LogLevel.Warn, "BUS 투입 차단", "BUS 자동 투입 조건 불만족으로 차단됨", "BUS 투입이 차단되었습니다.");
    public static readonly LogDefinition BusApplyAborted = new("BUS-RUN-102", LogCategory.Operation, LogLevel.Error, "BUS 투입 중단", "BUS 자동 투입 중 오류로 중단됨", "BUS 투입이 중단되었습니다.");
    public static readonly LogDefinition BusApplySkipped = new("BUS-RUN-103", LogCategory.Operation, LogLevel.Warn, "BUS 투입 생략", "유효한 자동 투입 조합이 없어 생략됨", "BUS 투입이 생략되었습니다.");
    public static readonly LogDefinition BusStopAborted = new("BUS-STOP-101", LogCategory.Operation, LogLevel.Error, "BUS 정지 중단", "BUS 정지 중 오류로 중단됨", "BUS 정지가 중단되었습니다.");
    public static readonly LogDefinition ManualOnCompleted = new("BUS-MAN-001", LogCategory.Operation, LogLevel.Info, "수동 K 투입 완료", "수동 K 투입 완료", "수동 K 투입이 완료되었습니다.");
    public static readonly LogDefinition ManualOffCompleted = new("BUS-MAN-002", LogCategory.Operation, LogLevel.Info, "수동 K 정지 완료", "수동 K 정지 완료", "수동 K 정지가 완료되었습니다.");
    public static readonly LogDefinition ManualOutputOffCompleted = new("BUS-MAN-003", LogCategory.Operation, LogLevel.Info, "수동 출력 일괄 정지 완료", "TopLabelBorder 수동 정지 완료", "수동 출력 정지가 완료되었습니다.");
    public static readonly LogDefinition ManualControlBlocked = new("BUS-MAN-101", LogCategory.Operation, LogLevel.Warn, "수동 동작 차단", "수동 K 제어 조건 불만족으로 차단됨", "수동 동작이 차단되었습니다.");
    public static readonly LogDefinition ManualOutputControlBlocked = new("BUS-MAN-102", LogCategory.Operation, LogLevel.Warn, "수동 출력 정지 차단", "수동 출력 정지 조건 불만족으로 차단됨", "수동 출력 정지가 차단되었습니다.");
    public static readonly LogDefinition ManualControlSkipped = new("BUS-MAN-103", LogCategory.Operation, LogLevel.Warn, "수동 동작 생략", "다른 BUS 동작 중이라 수동 동작이 생략됨", "수동 동작이 생략되었습니다.");

    public static readonly LogDefinition InterlockBlocked = new("OCR-INT-001", LogCategory.Protection, LogLevel.Warn, "인터락 동작 차단", "동일 MC 그룹의 다른 K가 이미 투입되어 차단됨", "인터락으로 동작이 차단되었습니다.");
    public static readonly LogDefinition OnFeedbackConfirmed = new("OCR-FBK-001", LogCategory.Protection, LogLevel.Info, "투입 피드백 확인", "K 투입 후 피드백이 정상 확인됨", "투입 피드백이 정상 확인되었습니다.");
    public static readonly LogDefinition OnFeedbackMismatch = new("OCR-FBK-002", LogCategory.Protection, LogLevel.Warn, "투입 피드백 불일치", "K 투입 후 피드백이 일치하지 않음", "투입 피드백이 일치하지 않습니다.");
    public static readonly LogDefinition OnFeedbackVerificationFailed = new("OCR-FBK-003", LogCategory.Protection, LogLevel.Error, "투입 피드백 확인 실패", "피드백 확인 중 예외가 발생함", "투입 피드백 확인에 실패했습니다.");
    public static readonly LogDefinition OnFeedbackRetryExhausted = new("OCR-FBK-004", LogCategory.Protection, LogLevel.Alarm, "투입 피드백 재시도 초과", "피드백 재시도 횟수 초과", "투입 피드백 재시도 횟수를 초과했습니다.");
    public static readonly LogDefinition OffFeedbackConfirmed = new("OCR-FBK-005", LogCategory.Protection, LogLevel.Info, "정지 피드백 확인", "K 정지 후 피드백 OFF가 정상 확인됨", "정지 피드백이 정상 확인되었습니다.");
    public static readonly LogDefinition OffFeedbackMismatch = new("OCR-FBK-006", LogCategory.Protection, LogLevel.Warn, "정지 피드백 불일치", "K 정지 후 피드백이 일치하지 않음", "정지 피드백이 일치하지 않습니다.");
    public static readonly LogDefinition OffFeedbackVerificationFailed = new("OCR-FBK-007", LogCategory.Protection, LogLevel.Error, "정지 피드백 확인 실패", "정지 피드백 확인 중 예외가 발생함", "정지 피드백 확인에 실패했습니다.");
    public static readonly LogDefinition OffFeedbackRetryExhausted = new("OCR-FBK-008", LogCategory.Protection, LogLevel.Alarm, "정지 피드백 재시도 초과", "정지 피드백 재시도 횟수 초과", "정지 피드백 재시도 횟수를 초과했습니다.");

    public static readonly LogDefinition LineSimulatorConnectFailed = new("COM-PLC-001", LogCategory.Communication, LogLevel.Error, "Line Simulator 연결 실패", "Line Simulator Modbus TCP 연결 실패", "Line Simulator 연결에 실패했습니다.");
    public static readonly LogDefinition LineSimulatorConnected = new("COM-PLC-002", LogCategory.Communication, LogLevel.Info, "Line Simulator 연결 성공", "Line Simulator Modbus TCP 연결 성공", "Line Simulator 연결이 완료되었습니다.");
    public static readonly LogDefinition LineRegisterReadReady = new("COM-PLC-003", LogCategory.Communication, LogLevel.Info, "Line Simulator 레지스터 읽기 준비", "Line Simulator 레지스터 읽기 성공", "Line Simulator 레지스터를 읽었습니다.");
    public static readonly LogDefinition LineRegisterReadFailed = new("COM-PLC-004", LogCategory.Communication, LogLevel.Error, "Line Simulator 레지스터 읽기 실패", "Line Simulator 레지스터 읽기 실패", "Line Simulator 레지스터 읽기에 실패했습니다.");
    public static readonly LogDefinition LineSimulatorDisconnected = new("COM-PLC-005", LogCategory.Communication, LogLevel.Warn, "Line Simulator 연결 해제", "Line Simulator 연결이 해제됨", "Line Simulator 연결이 해제되었습니다.");
    public static readonly LogDefinition FeedbackReadFailed = new("COM-PLC-006", LogCategory.Communication, LogLevel.Error, "Discrete Input 읽기 실패", "K 피드백 읽기 실패", "피드백 읽기에 실패했습니다.");
    public static readonly LogDefinition FeedbackRecovered = new("COM-PLC-007", LogCategory.Communication, LogLevel.Info, "Discrete Input 읽기 복구", "K 피드백 읽기 복구", "피드백 읽기가 복구되었습니다.");
    public static readonly LogDefinition CoilWrite = new("COM-PLC-008", LogCategory.Communication, LogLevel.Info, "Coil 쓰기", "K Coil 쓰기 성공", "Coil 쓰기를 수행했습니다.");
    public static readonly LogDefinition CoilWriteFailed = new("COM-PLC-009", LogCategory.Communication, LogLevel.Error, "Coil 쓰기 실패", "K Coil 쓰기 실패", "Coil 쓰기에 실패했습니다.");
    public static readonly LogDefinition EndpointPollingStarted = new("COM-END-001", LogCategory.Communication, LogLevel.Info, "EndPoint 폴링 시작", "OCR/PM EndPoint 폴링 시작", "EndPoint 폴링이 시작되었습니다.");
    public static readonly LogDefinition EndpointPollingStopped = new("COM-END-002", LogCategory.Communication, LogLevel.Info, "EndPoint 폴링 정지", "OCR/PM EndPoint 폴링 정지", "EndPoint 폴링이 정지되었습니다.");
    public static readonly LogDefinition EndpointRegisterWrite = new("COM-END-003", LogCategory.Communication, LogLevel.Info, "EndPoint 단일 레지스터 쓰기", "EndPoint 단일 레지스터 쓰기 성공", "EndPoint 단일 레지스터 쓰기를 수행했습니다.");
    public static readonly LogDefinition EndpointRegisterWriteFailed = new("COM-END-004", LogCategory.Communication, LogLevel.Error, "EndPoint 단일 레지스터 쓰기 실패", "EndPoint 단일 레지스터 쓰기 실패", "EndPoint 단일 레지스터 쓰기에 실패했습니다.");
    public static readonly LogDefinition EndpointRegisterBlockWrite = new("COM-END-005", LogCategory.Communication, LogLevel.Info, "EndPoint 다중 레지스터 쓰기", "EndPoint 다중 레지스터 쓰기 성공", "EndPoint 다중 레지스터 쓰기를 수행했습니다.");
    public static readonly LogDefinition EndpointRegisterBlockWriteFailed = new("COM-END-006", LogCategory.Communication, LogLevel.Error, "EndPoint 다중 레지스터 쓰기 실패", "EndPoint 다중 레지스터 쓰기 실패", "EndPoint 다중 레지스터 쓰기에 실패했습니다.");

    public static readonly LogDefinition EndpointSettingsApplied = new("SET-END-001", LogCategory.SettingChange, LogLevel.Info, "EndPoint 설정 적용", "OCR/PM EndPoint 설정 적용", "EndPoint 설정이 적용되었습니다.");
    public static readonly LogDefinition EndpointSettingsEmpty = new("SET-END-002", LogCategory.SettingChange, LogLevel.Warn, "EndPoint 설정 적용 대상 없음", "선택된 OCR/PM EndPoint 없음", "적용할 EndPoint가 없습니다.");

    public static readonly LogDefinition AlarmRetryExhausted = new("ALM-PLC-001", LogCategory.Alarm, LogLevel.Alarm, "알람 코일 ON 요청", "피드백 재시도 초과로 알람 코일 ON 요청", "알람 코일 ON 요청이 발생했습니다.");
    public static readonly LogDefinition AlarmCoilWriteSkipped = new("ALM-PLC-002", LogCategory.Alarm, LogLevel.Warn, "알람 코일 쓰기 생략", "연결 해제로 알람 코일 쓰기 생략", "알람 코일 쓰기가 생략되었습니다.");
    public static readonly LogDefinition AlarmCoilWriteFailed = new("ALM-PLC-003", LogCategory.Alarm, LogLevel.Error, "알람 코일 쓰기 실패", "알람 코일 쓰기 실패", "알람 코일 쓰기에 실패했습니다.");

    public static IReadOnlyList<LogDefinition> All { get; } =
    [
        SystemAutoModeChanged,
        SystemManualModeChanged,
        PlanCalculated,
        PlanCalculationFailed,
        DeviceDetailRequested,
        DeviceDetailOpenFailed,
        UserOperationCancelled,
        Bus1Applied,
        Bus2Applied,
        Bus3Applied,
        Bus1Stopped,
        Bus2Stopped,
        Bus3Stopped,
        BusApplyBlocked,
        BusApplyAborted,
        BusApplySkipped,
        BusStopAborted,
        ManualOnCompleted,
        ManualOffCompleted,
        ManualOutputOffCompleted,
        ManualControlBlocked,
        ManualOutputControlBlocked,
        ManualControlSkipped,
        InterlockBlocked,
        OnFeedbackConfirmed,
        OnFeedbackMismatch,
        OnFeedbackVerificationFailed,
        OnFeedbackRetryExhausted,
        OffFeedbackConfirmed,
        OffFeedbackMismatch,
        OffFeedbackVerificationFailed,
        OffFeedbackRetryExhausted,
        LineSimulatorConnectFailed,
        LineSimulatorConnected,
        LineRegisterReadReady,
        LineRegisterReadFailed,
        LineSimulatorDisconnected,
        FeedbackReadFailed,
        FeedbackRecovered,
        CoilWrite,
        CoilWriteFailed,
        EndpointPollingStarted,
        EndpointPollingStopped,
        EndpointRegisterWrite,
        EndpointRegisterWriteFailed,
        EndpointRegisterBlockWrite,
        EndpointRegisterBlockWriteFailed,
        EndpointSettingsApplied,
        EndpointSettingsEmpty,
        AlarmRetryExhausted,
        AlarmCoilWriteSkipped,
        AlarmCoilWriteFailed
    ];

    public static LogDefinition GetBusApplied(string busName) =>
        busName switch
        {
            "BUS1" => Bus1Applied,
            "BUS2" => Bus2Applied,
            "BUS3" => Bus3Applied,
            _ => BusApplyAborted
        };

    public static LogDefinition GetBusStopped(string busName) =>
        busName switch
        {
            "BUS1" => Bus1Stopped,
            "BUS2" => Bus2Stopped,
            "BUS3" => Bus3Stopped,
            _ => BusStopAborted
        };
}
