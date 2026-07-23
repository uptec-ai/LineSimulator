using DevExpress.Mvvm;
using DevExpress.Utils.Filtering;
using DevExpress.Xpf.Bars.Native;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Data;
using TestMcAlgorithm.Models;
using TestMcAlgorithm.Services;
using static TestMcAlgorithm.Models.OvrEndpointSettingsModel;

namespace TestMcAlgorithm.ViewModels;

public sealed class LineSimulatorViewModel : ObservableObject, IDisposable
{
    // address mapping

    private const ushort LineRegisterStartAddress = 0;
    private const ushort LineRegisterCount = 6;

    private const ushort EndpointRegisterStartAddress = 476;
    private const ushort EndpointRegisterCount = 26;

    public BusDiagram BusDiagram { get; }
    private const int MaxApplyFeedbackRetryCount = 3;
    private const ushort AlarmCoilAddress = 100;

    private readonly McAlgorithmService _algorithmService;
    private readonly IModbusGatewayService _modbusGatewayService;
    private readonly SemaphoreSlim _modbusIoLock = new(1, 1); // 최대 작업 : 1개
    private bool _isRefreshingSelections;
    private bool _isBusOperationRunning;
    private CancellationTokenSource? _feedbackPollingCts;
    private Task? _feedbackPollingTask;
    private CancellationTokenSource? _ovrPollingCts;
    private Task? _ovrPollingTask;
    private CancellationTokenSource? _idleMonitorCts;
    private Task? _idleMonitorTask;
    private bool _isShuttingDown;
    private bool _feedbackReadFailed;
    private bool _isRevertingOperationMode;

    private IReadOnlyList<BusRequestSpec> _availableBus2Requests = [];
    private IReadOnlyList<BusRequestSpec> _availableBus3Requests = [];
    private AlgorithmPlan? _currentPlan;

    private float _nbusOut1;
    public float    NBusOut1
    {
        get => _nbusOut1;
        set => SetProperty(ref _nbusOut1, value);
    }
    private float _nbusOut2;
    public float NBusOut2
    {
        get => _nbusOut2;
        set => SetProperty(ref _nbusOut2, value);
    }
    private float _nbusOut3;
    public float NBusOut3
    {
        get => _nbusOut3;
        set => SetProperty(ref _nbusOut3, value);
    }

    #region MainWindow Composition
    public LineSimulatorViewModel(McAlgorithmService algorithmService, IModbusGatewayService modbusGatewayService)
    {
        _algorithmService = algorithmService;
        _modbusGatewayService = modbusGatewayService;
        BusDiagram = new BusDiagram();
        BusDiagram.KBusClickRequestedAsync = HandleDiagramKBusClickAsync;
        BusDiagram.OutputClickRequestedAsync = HandleDiagramOutputClickAsync;
        BusDiagram.MarkerClickRequestedAsync = HandleDiagramMarkerClickAsync;
        State = new MainScreenStateModel();
        LogStore = new LogStore();
        McItems = new ObservableCollection<McButtonViewModel>(McCatalog.All.Select(definition => new McButtonViewModel(definition)));
        KItems = new ObservableCollection<KContactViewModel>(KCatalog.All.OrderBy(definition => definition.Number).Select(definition => new KContactViewModel(definition)));
        Logs = LogStore.RecentEntries;

        Bus1RatedOptions = new ObservableCollection<double>(_algorithmService.SupportedRatedKva);
        Bus1ScrOptions = new ObservableCollection<double>(_algorithmService.SupportedScr);
        Bus2RatedOptions = new ObservableCollection<double>();
        Bus2ScrOptions = new ObservableCollection<double>();
        Bus3RatedOptions = new ObservableCollection<double>();
        Bus3ScrOptions = new ObservableCollection<double>();

        ConnectCommand = new AsyncRelayCommand(_ => ConnectAsync(), _ => !State.Connection.IsConnected);
        DisconnectCommand = new AsyncRelayCommand(_ => DisconnectAsync(), _ => State.Connection.IsConnected);
        ClearLogsCommand = new RelayCommand(_ => LogStore.Clear());
        ApplyOvrSettingsCommand = new AsyncRelayCommand(_ => ApplyOvrSettingsAsync());

        Bus1ApplyCommand = new AsyncRelayCommand(_ => TurnBusOnAsync("BUS1"), _ => CanApplyBus("BUS1"));
        Bus2ApplyCommand = new AsyncRelayCommand(_ => TurnBusOnAsync("BUS2"), _ => CanApplyBus("BUS2"));
        Bus3ApplyCommand = new AsyncRelayCommand(_ => TurnBusOnAsync("BUS3"), _ => CanApplyBus("BUS3"));
        Bus1OffCommand = new AsyncRelayCommand(_ => TurnBusOffAsync("BUS1"), _ => CanOffBus("BUS1"));
        Bus2OffCommand = new AsyncRelayCommand(_ => TurnBusOffAsync("BUS2"), _ => CanOffBus("BUS2"));
        Bus3OffCommand = new AsyncRelayCommand(_ => TurnBusOffAsync("BUS3"), _ => CanOffBus("BUS3"));

        SubscribeStateEvents();
        RefreshBusAvailability();
        SyncBusDiagramFeedback();
        InitializeEndpointDefaults();
        AutoCalculatePlan();
        StartIdleMonitor();
    }
    #endregion

    #region EndPoint Defaults
    private void InitializeEndpointDefaults()
    {

        // 엔드포인트 IP/포트는 App.config(appSettings)의 "{DeviceKey}_Ip" / "{DeviceKey}_Port"
        // 키에서만 관리한다 (OCR1~OCR10, PM1~PM4). 키가 없으면 모델 기본값(127.0.0.1:502)을 유지한다.
        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            endpoint.IpAddress = AppConfig.GetString($"{endpoint.DeviceKey}_Ip", endpoint.IpAddress);
            endpoint.Port = AppConfig.GetInt($"{endpoint.DeviceKey}_Port", endpoint.Port);
        }

        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            if (!endpoint.IsOvr)
            {
                continue;
            }

            endpoint.CurrentRegisterAddress = 500; // EOCR 기본 전류 레지스터 주소
            endpoint.CurrentScale = 0.01; // EOCR 전류 스케일
        }
    }
    #endregion

    #region MainWindow State / Commands
    public MainScreenStateModel State { get; }
    public LogStore LogStore { get; }
    public ObservableCollection<McButtonViewModel> McItems { get; }
    public ObservableCollection<KContactViewModel> KItems { get; }
    public ObservableCollection<LogEntryModel> Logs { get; }
    public ObservableCollection<double> Bus1RatedOptions { get; }
    public ObservableCollection<double> Bus1ScrOptions { get; }
    public ObservableCollection<double> Bus2RatedOptions { get; }
    public ObservableCollection<double> Bus2ScrOptions { get; }
    public ObservableCollection<double> Bus3RatedOptions { get; }
    public ObservableCollection<double> Bus3ScrOptions { get; }

    public bool CanEditBus1Settings => State.OperationMode.IsAutoMode && !State.Bus1.IsConfigurationLocked;
    public bool CanEditBus2Settings => State.OperationMode.IsAutoMode && State.Bus2.IsEnabled && !State.Bus2.IsConfigurationLocked;
    public bool CanEditBus3Settings => State.OperationMode.IsAutoMode && State.Bus3.IsEnabled && !State.Bus3.IsConfigurationLocked;
    public bool CanEditBus2Usage => State.OperationMode.IsAutoMode && !State.Bus2.IsConfigurationLocked;
    public bool CanEditBus3Usage => State.OperationMode.IsAutoMode && State.Bus2.IsEnabled && !State.Bus3.IsConfigurationLocked;

    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public RelayCommand ClearLogsCommand { get; }
    public AsyncRelayCommand ApplyOvrSettingsCommand { get; }
    public AsyncRelayCommand Bus1ApplyCommand { get; }
    public AsyncRelayCommand Bus2ApplyCommand { get; }
    public AsyncRelayCommand Bus3ApplyCommand { get; }
    public AsyncRelayCommand Bus1OffCommand { get; }
    public AsyncRelayCommand Bus2OffCommand { get; }
    public AsyncRelayCommand Bus3OffCommand { get; }

    public event Action<string>? DeviceDetailRequested;

    public DeviceDetailWindowViewModel CreateDeviceDetailViewModel(string deviceKey)
    {
        var endpoint = FindEndpointOrThrow(deviceKey);
        return new DeviceDetailWindowViewModel(endpoint, endpoint.ProtocolProfile.ReadStartAddress);
    }

    private static ushort FeedbackStartAddress => KCatalog.All.Min(item => item.FeedbackAddress);
    private static ushort FeedbackCount => (ushort)(KCatalog.All.Max(item => item.FeedbackAddress) - FeedbackStartAddress + 1);
    private void SubscribeStateEvents()
    {
        State.Connection.PropertyChanged += OnConnectionPropertyChanged;
        State.Bus1.PropertyChanged += OnBus1PropertyChanged;
        State.Bus2.PropertyChanged += OnBus2PropertyChanged;
        State.Bus3.PropertyChanged += OnBus3PropertyChanged;
        State.OperationMode.PropertyChanged += OnOperationModePropertyChanged;
    }

    private void UnsubscribeStateEvents()
    {
        State.Connection.PropertyChanged -= OnConnectionPropertyChanged;
        State.Bus1.PropertyChanged -= OnBus1PropertyChanged;
        State.Bus2.PropertyChanged -= OnBus2PropertyChanged;
        State.Bus3.PropertyChanged -= OnBus3PropertyChanged;
        State.OperationMode.PropertyChanged -= OnOperationModePropertyChanged;
    }

    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionSettingsModel.IsConnected))
        {
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
            RaiseBusCommandCanExecuteChanged();
        }
    }

    private void OnOperationModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OperationModeModel.IsManualMode))
        {
            return;
        }

        if (_isRevertingOperationMode)
        {
            return;
        }

        if (!AreAllBusOutputsOpen())
        {
            _isRevertingOperationMode = true;
            State.OperationMode.IsManualMode = !State.OperationMode.IsManualMode;
            _isRevertingOperationMode = false;
            MessageBox.Show($"투입된 버스를 먼저 해제하세요.", "동작 모드 변경", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RaiseModeAndConfigurationStateChanged();
        AddLog(
            State.OperationMode.IsManualMode ? LogDefinitions.SystemManualModeChanged : LogDefinitions.SystemAutoModeChanged,
            $"Operation mode changed: {(State.OperationMode.IsManualMode ? "Manual" : "Auto")}");
    }

    private void OnBus1PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BusSelectionModel.IsApplied))
        {
            RaiseBusCommandCanExecuteChanged();
        }

        if (e.PropertyName == nameof(BusSelectionModel.IsConfigurationLocked))
        {
            RaiseModeAndConfigurationStateChanged();
        }

        if (_isRefreshingSelections)
        {
            return;
        }

        if (e.PropertyName is nameof(BusSelectionModel.RatedKva) or nameof(BusSelectionModel.Scr))
        {
            _currentPlan = null;
            RefreshBusAvailability();
            AutoCalculatePlan();
        }
    }

    private void OnBus2PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRefreshingSelections)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(BusSelectionModel.IsEnabled):
                _currentPlan = null;
                if (!State.Bus2.IsEnabled && State.Bus3.IsEnabled)
                {
                    State.Bus3.IsEnabled = false;
                }

                RefreshBus3Availability();
                RaiseModeAndConfigurationStateChanged();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.IsApplied):
                RaiseBusCommandCanExecuteChanged();
                break;

            case nameof(BusSelectionModel.IsConfigurationLocked):
                RaiseModeAndConfigurationStateChanged();
                break;

            case nameof(BusSelectionModel.RatedKva):
                _currentPlan = null;
                RefreshBus2ScrOptionsOnly();
                RefreshBus3Availability();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.Scr):
                _currentPlan = null;
                RefreshBus3Availability();
                AutoCalculatePlan();
                break;
        }
    }

    private void OnBus3PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRefreshingSelections)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(BusSelectionModel.IsEnabled):
                if (State.Bus3.IsEnabled && !State.Bus2.IsEnabled)
                {
                    State.Bus3.IsEnabled = false;
                    return;
                }

                _currentPlan = null;
                RaiseModeAndConfigurationStateChanged();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.IsApplied):
                RaiseBusCommandCanExecuteChanged();
                break;

            case nameof(BusSelectionModel.IsConfigurationLocked):
                RaiseModeAndConfigurationStateChanged();
                break;

            case nameof(BusSelectionModel.RatedKva):
                _currentPlan = null;
                RefreshBus3ScrOptionsOnly();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.Scr):
                _currentPlan = null;
                AutoCalculatePlan();
                break;
        }
    }

    #endregion

    #region LineSimulator Connection
    private async Task ConnectAsync()
    {
        try
        {
            await StopFeedbackPollingAsync();
            await _modbusGatewayService.ConnectAsync(State.Connection.IpAddress, State.Connection.Port, CancellationToken.None); // line simulator 
            State.Connection.IsConnected = true;
            _feedbackReadFailed = false;
            AddLog(LogDefinitions.LineSimulatorConnected, $"Connected to {State.Connection.IpAddress}:{State.Connection.Port}");

            StartFeedbackPolling();
        }
        catch (Exception ex)
        {
            AddLog(LogDefinitions.LineSimulatorConnectFailed, $"Connect failed: {ex.Message}");
        }
    }

    private async Task DisconnectAsync()
    {
        await DisconnectCoreAsync("Disconnected", stopPolling: true);
    }

    public async Task DisconnectForCloseAsync()
    {
        if (!State.Connection.IsConnected)
        {
            return;
        }

        await DisconnectAsync();
    }

    public ModbusMonitoringSnapshot CreateMonitoringSnapshot()
    {
        return new ModbusMonitoringSnapshot(
            State.Connection.IsConnected,
            State.OperationMode.IsManualMode,
            State.Bus1.IsEnabled,
            State.Bus2.IsEnabled,
            State.Bus3.IsEnabled,
            State.Bus1.IsApplied,
            State.Bus2.IsApplied,
            State.Bus3.IsApplied,
            NBusOut1,
            NBusOut2,
            NBusOut3,
            KItems.ToDictionary(item => item.Code, item => item.IsOn));
    }
    #endregion

    #region MainWindow Bus Planning / Operations
    private void CalculatePlan(bool writeLog = true)
    {
        try
        {
            _currentPlan = BuildPlan();
            ApplyPlanToView(_currentPlan);
            if (writeLog)
            {
                AddLog(LogDefinitions.PlanCalculated, "Algorithm plan calculated.");
            }
        }
        catch (Exception ex)
        {
            State.Bus1.Summary = BuildBusSummary("BUS1", null, isEnabled: true);
            State.Bus2.Summary = BuildBusSummary("BUS2", null, State.Bus2.IsEnabled);
            State.Bus3.Summary = BuildBusSummary("BUS3", null, State.Bus3.IsEnabled);
            State.AlgorithmSummary = ex.Message;
            if (writeLog)
            {
                AddLog(LogDefinitions.PlanCalculationFailed, $"Calculate failed: {ex.Message}");
            }
        }
    }

    private void AutoCalculatePlan()
    {
        if (_isRefreshingSelections)
        {
            return;
        }

        CalculatePlan(writeLog: false);
    }

    private async Task TurnBusOnAsync(string busName)
    {
        if (!TryBeginBusOperation()) return;

        try
        {
            if (!State.Connection.IsConnected)
            {
                AddLog(LogDefinitions.BusApplyBlocked, $"{busName} apply blocked: Line Simulator disconnected.");
                return;
            }

            try
            {
                _currentPlan ??= BuildPlan();
            }
            catch (Exception ex)
            {
                AddLog(LogDefinitions.BusApplyAborted, $"Apply aborted: {ex.Message}");
                return;
            }

            var previousAssignments = McItems
                .Where(item => item.IsAlgorithmManaged && item.AssignedBus == busName)
                .Select(item => item.Number)
                .ToHashSet();

            ApplyPlanToView(_currentPlan); // 새 계획을 뷰에 먼저 적용하여, 사용자에게 변경 내용을 명확히 보여주고, 이후 실제 ON/OFF 작업에서는 이 계획을 기준으로 피드백을 확인하도록 함
            var target = GetBusResult(_currentPlan, busName);
            if (target is null || !target.IsAssigned)
            {
                AddLog(LogDefinitions.BusApplySkipped, $"{busName} apply skipped: valid selection 없음");
                return;
            }

            SetBusConfigurationLocked(busName, true);

            var selected = target.McNumbers.ToHashSet();
            var selectedByAnyBus = _currentPlan.OrderedTurnOnNumbers.ToHashSet();

            foreach (var number in previousAssignments.Where(number => !selected.Contains(number) && !selectedByAnyBus.Contains(number)).OrderByDescending(number => number)) // 현재 버스에 할당되어 있지만, 새 계획에서 어떤 버스에도 할당되지 않은 MC는 먼저 OFF
            {
                var kItem = ResolveKItem(number, busName);
                var cleared = await TurnKOffWithFeedbackCheckAsync(kItem, $"Algorithm clear {kItem.Code}");
                if (!cleared)
                {
                    SetBusConfigurationLocked(busName, false);
                    AddLog(LogDefinitions.BusApplyAborted, $"{busName} apply aborted: {kItem.Code} clear feedback not confirmed.");
                    return;
                }
            }

            foreach (var number in target.McNumbers.OrderBy(number => number)) // 새 계획에서 할당된 MC는 번호 순서대로 ON (일반적으로 낮은 번호가 높은 우선순위이므로)
            {
                var kItem = ResolveKItem(number, busName);
                var applied = await ApplyKWithFeedbackCheckAsync(kItem, busName);
                if (!applied)
                {
                    SetBusConfigurationLocked(busName, false);
                    AddLog(LogDefinitions.BusApplyAborted, $"{busName} apply aborted: {kItem.Code} feedback not confirmed.");
                    return;
                }
            }

            SetBusApplied(busName, true);
            AddLog(LogDefinitions.GetBusApplied(busName), $"{busName} apply sequence completed.");
        }
        finally
        {
            EndBusOperation();
        }
    }

    private static BusSelectionResult? GetBusResult(AlgorithmPlan plan, string busName)
    {
        return busName switch
        {
            "BUS1" => plan.Bus1,
            "BUS2" => plan.Bus2,
            "BUS3" => plan.Bus3,
            _ => null,
        };
    }

    private static string? ResolveOutputTargetBus(string outputTitle)
    {
        return outputTitle switch
        {
            "BUS OUT #1" => "BUS1",
            "BUS OUT #2" => "BUS2",
            "BUS OUT #3" => "BUS3",
            "NBUS OUT #1" => "NBUS1",
            "NBUS OUT #2" => "NBUS2",
            "NBUS OUT #3" => "NBUS3",
            _ => null
        };
    }

    private async Task TurnBusOffAsync(string busName)
    {
        if (!TryBeginBusOperation())
        {
            return;
        }

        try
        {
            if (!State.Connection.IsConnected)
            {
                AddLog(LogDefinitions.BusStopAborted, $"{busName} stop blocked: Line Simulator disconnected.");
                return;
            }

            if (!HasActiveBusOutput(busName))
            {
                AddLog(LogDefinitions.BusStopAborted, $"{busName} stop skipped: no active KBus found.");
                return;
            }

            var stopped = await TurnSingleBusOffAsync(busName);
            if (!stopped)
            {
                return;
            }

            _currentPlan = null;
        }
        finally
        {
            EndBusOperation();
        }
    }

    private async Task<bool> TurnSingleBusOffAsync(string busName)
    {
        var targetItems = KItems
            .Where(item => string.Equals(item.TargetBus, busName, StringComparison.OrdinalIgnoreCase) && item.IsOn)
            .OrderByDescending(item => item.Number)
            .ToArray();

        foreach (var kItem in targetItems)
        {
            var turnedOff = await TurnKOffWithFeedbackCheckAsync(kItem, $"{busName} off");
            if (!turnedOff)
            {
                AddLog(LogDefinitions.BusStopAborted, $"{busName} off aborted: {kItem.Code} feedback not confirmed.");
                return false;
            }
        }

        foreach (var mc in McItems.Where(item => item.AssignedBus == busName))
        {
            mc.AssignedBus = "-";
        }

        SetBusConfigurationLocked(busName, false);
        SetBusApplied(busName, false);
        AddLog(LogDefinitions.GetBusStopped(busName), $"{busName} off sequence completed.");
        return true;
    }

    private async Task HandleDiagramKBusClickAsync(string kCode)
    {
        if (!TryBeginBusOperation())
        {
            AddLog(LogDefinitions.ManualControlSkipped, "Manual control skipped: another operation is running.");
            return;
        }

        try
        {
            if (!State.Connection.IsConnected)
            {
                AddLog(LogDefinitions.ManualControlBlocked, $"{kCode} manual control blocked: Line Simulator disconnected.");
                return;
            }

            if (!State.OperationMode.IsManualMode)
            {
                AddLog(LogDefinitions.ManualControlBlocked, $"{kCode} manual control blocked: switch to Manual mode first.");
                return;
            }

            var kItem = KItems.FirstOrDefault(item => string.Equals(item.Code, kCode, StringComparison.OrdinalIgnoreCase));
            if (kItem is null)
            {
                AddLog(LogDefinitions.ManualControlBlocked, $"{kCode} manual control unavailable: K mapping not found.");
                return;
            }

            var operationBus = kItem.TargetBus;

            var actionText = kItem.IsOn ? "해제" : "동작";
            var confirmationResult = MessageBox.Show(
                $"{kCode} 버스를 {actionText} 하시겠습니까?",
                "수동 동작 확인",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirmationResult != MessageBoxResult.OK)
            {
                AddLog(LogDefinitions.UserOperationCancelled, $"{kCode} manual control cancelled by user.");
                return;
            }

            if (kItem.IsOn)
            {
                var turnedOff = await TurnKOffWithFeedbackCheckAsync(kItem, $"{operationBus} manual off");
                if (turnedOff)
                {
                    AddLog(LogDefinitions.ManualOffCompleted, $"{kCode} manual off completed.");
                }

                return;
            }

            var interlockedPeer = FindInterlockedPeer(kItem);
            if (interlockedPeer is not null)
            {
                AddLog(LogDefinitions.InterlockBlocked, $"{kCode} manual on blocked: {interlockedPeer.Code} is already ON for MC{interlockedPeer.Definition.SourceMcNumber}.");
                return;
            }

            var applied = await ApplyKWithFeedbackCheckAsync(kItem, $"{operationBus} manual");
            if (applied)
            {
                AddLog(LogDefinitions.ManualOnCompleted, $"{kCode} manual on completed.");
            }
        }
        finally
        {
            EndBusOperation();
        }
    }

    private Task HandleDiagramMarkerClickAsync(string deviceKey)
    {
        try
        {
            var endpoint = FindEndpointOrThrow(deviceKey);
            DeviceDetailRequested?.Invoke(endpoint.DeviceKey);
            AddLog(LogDefinitions.DeviceDetailRequested, $"{endpoint.DeviceKey} detail requested.");
        }
        catch (Exception ex)
        {
            AddLog(LogDefinitions.DeviceDetailOpenFailed, $"Device detail open failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task HandleDiagramOutputClickAsync(string outputTitle)
    {
        if (!TryBeginBusOperation())
        {
            AddLog(LogDefinitions.ManualControlSkipped, "output control skipped: another operation is running.");
            return;
        }

        try
        {
            if (!State.Connection.IsConnected)
            {
                AddLog(LogDefinitions.ManualOutputControlBlocked, $"{outputTitle} off blocked: Line Simulator disconnected.");
                return;
            }

            //if (!State.OperationMode.IsManualMode)
            //{
            //    AddLog(LogDefinitions.ManualOutputControlBlocked, $"{outputTitle} manual off blocked: switch to Manual mode first.");
            //    return;
            //}

            var targetBus = ResolveOutputTargetBus(outputTitle);
            if (targetBus is null)
            {
                AddLog(LogDefinitions.ManualOutputControlBlocked, $"{outputTitle} off skipped: output mapping not found.");
                return;
            }

            var targetItems = KItems
                .Where(item => string.Equals(item.TargetBus, targetBus, StringComparison.OrdinalIgnoreCase) && item.IsOn)
                .OrderByDescending(item => item.Definition.CoilAddress)
                .ToArray();

            if (targetItems.Length == 0)
            {
                AddLog(LogDefinitions.ManualOutputControlBlocked, $"{outputTitle} off skipped: no active K found.");
                return;
            }

            foreach (var kItem in targetItems)
            {
                var turnedOff = await TurnKOffWithFeedbackCheckAsync(kItem, $"{targetBus} output off");
                if (!turnedOff)
                {
                    AddLog(LogDefinitions.BusStopAborted, $"{outputTitle} off aborted: {kItem.Code} feedback not confirmed.");
                    return;
                }
            }

            AddLog(LogDefinitions.ManualOutputOffCompleted, $"{outputTitle} manual off completed.");
        }
        finally
        {
            EndBusOperation();
        }
    }

    private AlgorithmPlan BuildPlan()
    {
        var bus1 = new BusRequestSpec("BUS1", State.Bus1.RatedKva, State.Bus1.Scr);
        if (!_algorithmService.CanBuildBus1(bus1))
        {
            throw new InvalidOperationException($"BUS1 조합 없음: {State.Bus1.RatedKva:g}kVA / SCR {State.Bus1.Scr:g}");
        }

        BusRequestSpec? bus2 = State.Bus2.IsEnabled && HasValidSelection(_availableBus2Requests, State.Bus2.RatedKva, State.Bus2.Scr)
            ? new BusRequestSpec("BUS2", State.Bus2.RatedKva, State.Bus2.Scr)
            : null;

        BusRequestSpec? bus3 = State.Bus3.IsEnabled &&
                               bus2 is not null &&
                               HasValidSelection(_availableBus3Requests, State.Bus3.RatedKva, State.Bus3.Scr)
            ? new BusRequestSpec("BUS3", State.Bus3.RatedKva, State.Bus3.Scr)
            : null;

        return _algorithmService.BuildPlan(bus1, bus2, bus3);
    }

    private void RefreshBusAvailability()
    {
        _isRefreshingSelections = true;
        try
        {
            RefreshBus2Availability();
            RefreshBus3Availability();
        }
        finally
        {
            _isRefreshingSelections = false;
        }
    }

    private void RefreshBus2Availability()
    {
        _availableBus2Requests = [];
        Bus2RatedOptions.Clear();
        Bus2ScrOptions.Clear();

        var bus1 = new BusRequestSpec("BUS1", State.Bus1.RatedKva, State.Bus1.Scr);
        if (!_algorithmService.CanBuildBus1(bus1))
        {
            if (State.Bus3.IsEnabled)
            {
                State.Bus3.IsEnabled = false;
            }

            return;
        }

        _availableBus2Requests = _algorithmService.GetAvailableBus2Requests(bus1);
        PopulateRatedOptions(Bus2RatedOptions, _availableBus2Requests);
        EnsureSelectedRated(Bus2RatedOptions, value => State.Bus2.RatedKva = value, State.Bus2.RatedKva);
        RefreshBus2ScrOptionsOnly();
    }

    private void RefreshBus3Availability()
    {
        _availableBus3Requests = [];
        Bus3RatedOptions.Clear();
        Bus3ScrOptions.Clear();

        if (!State.Bus2.IsEnabled || !HasValidSelection(_availableBus2Requests, State.Bus2.RatedKva, State.Bus2.Scr))
        {
            if (State.Bus3.IsEnabled)
            {
                State.Bus3.IsEnabled = false;
            }

            return;
        }

        var bus1 = new BusRequestSpec("BUS1", State.Bus1.RatedKva, State.Bus1.Scr);
        var bus2 = new BusRequestSpec("BUS2", State.Bus2.RatedKva, State.Bus2.Scr);
        _availableBus3Requests = _algorithmService.GetAvailableBus3Requests(bus1, bus2);

        PopulateRatedOptions(Bus3RatedOptions, _availableBus3Requests);
        EnsureSelectedRated(Bus3RatedOptions, value => State.Bus3.RatedKva = value, State.Bus3.RatedKva);
        RefreshBus3ScrOptionsOnly();

        if (_availableBus3Requests.Count == 0 && State.Bus3.IsEnabled)
        {
            State.Bus3.IsEnabled = false;
        }
    }

    private void RefreshBus2ScrOptionsOnly()
    {
        Bus2ScrOptions.Clear();
        if (_availableBus2Requests.Count == 0)
        {
            return;
        }

        PopulateScrOptions(Bus2ScrOptions, _availableBus2Requests, State.Bus2.RatedKva);
        EnsureSelectedScr(Bus2ScrOptions, value => State.Bus2.Scr = value, State.Bus2.Scr);
    }

    private void RefreshBus3ScrOptionsOnly()
    {
        Bus3ScrOptions.Clear();
        if (_availableBus3Requests.Count == 0)
        {
            return;
        }

        PopulateScrOptions(Bus3ScrOptions, _availableBus3Requests, State.Bus3.RatedKva);
        EnsureSelectedScr(Bus3ScrOptions, value => State.Bus3.Scr = value, State.Bus3.Scr);
    }

    private static bool HasValidSelection(IReadOnlyList<BusRequestSpec> requests, double ratedKva, double scr)
    {
        return requests.Any(request => request.RatedKva.Equals(ratedKva) && request.Scr.Equals(scr));
    }

    private void PopulateRatedOptions(ObservableCollection<double> target, IReadOnlyList<BusRequestSpec> requests)
    {
        var values = _algorithmService.SupportedRatedKva
            .Where(rated => requests.Any(request => request.RatedKva.Equals(rated)))
            .ToArray();

        ReplaceItems(target, values);
    }

    private void PopulateScrOptions(ObservableCollection<double> target, IReadOnlyList<BusRequestSpec> requests, double ratedKva)
    {
        var values = _algorithmService.SupportedScr
            .Where(scr => requests.Any(request => request.RatedKva.Equals(ratedKva) && request.Scr.Equals(scr)))
            .ToArray();

        ReplaceItems(target, values);
    }

    private static void EnsureSelectedRated(ObservableCollection<double> options, Action<double> applySelection, double currentValue)
    {
        if (options.Count == 0)
        {
            return;
        }

        if (!options.Contains(currentValue))
        {
            applySelection(options[0]);
        }
    }

    private static void EnsureSelectedScr(ObservableCollection<double> options, Action<double> applySelection, double currentValue)
    {
        if (options.Count == 0)
        {
            return;
        }

        if (!options.Contains(currentValue))
        {
            applySelection(options[0]);
        }
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private void ApplyPlanToView(AlgorithmPlan plan)
    {
        ClearAssignments();
        ClearKDisplayAssignments();

        State.Bus1.Summary = BuildBusSummary("BUS1", plan.Bus1, isEnabled: true);
        State.Bus2.Summary = BuildBusSummary("BUS2", plan.Bus2, State.Bus2.IsEnabled);
        State.Bus3.Summary = BuildBusSummary("BUS3", plan.Bus3, State.Bus3.IsEnabled);
        State.AlgorithmSummary = plan.Explanation;

        AssignBus(plan.Bus1, "BUS1");

        if (plan.Bus2 is { IsAssigned: true })
        {
            AssignBus(plan.Bus2, "BUS2");
        }

        if (plan.Bus3 is { IsAssigned: true })
        {
            AssignBus(plan.Bus3, "BUS3");
        }
    }

    private void AssignBus(BusSelectionResult result, string busName)
    {
        foreach (var number in result.McNumbers)
        {
            McItems.First(item => item.Number == number).AssignedBus = busName;
            SetKDisplayAssignment(number, busName);
        }
    }

    private static string BuildBusSummary(string busName, BusSelectionResult? result, bool isEnabled)
    {
        if (!isEnabled || result is null || !result.IsAssigned || result.McNumbers.Count == 0)
        {
            return $"{busName}: disable";
        }
        var kCodes = result.McNumbers
            .Select(number => KCatalog.ByMcBus.TryGetValue((number, busName), out var definition) ? definition.Code : $"MC{number}")
            .ToArray();

        return $"투입: {string.Join(", ", kCodes)} / Zinq: {result.ZeqMohm.ToString("0.0")} mΩ";
    }

    private void ClearAssignments()
    {
        foreach (var mc in McItems)
        {
            mc.AssignedBus = "-";
        }
    }

    private void ClearKDisplayAssignments()
    {
        foreach (var kItem in KItems)
        {
            kItem.DisplayTargetBus = "-";
        }
    }


    private void SetKDisplayAssignment(int sourceMcNumber, string busName)
    {
        ResolveKItem(sourceMcNumber, busName).DisplayTargetBus = busName;
    }
    private KContactViewModel ResolveKItem(int sourceMcNumber, string busName)
    {
        return KItems.First(item => item.Definition.SourceMcNumber == sourceMcNumber && item.TargetBus == busName);
    }

    private async Task SetKStateAsync(KContactViewModel kItem, bool state, string reason, bool delayAfter = false)
    {
        try
        {
            await WriteSingleCoilAsync(
                  (byte)State.Connection.UnitId,
                  kItem.Definition.CoilAddress,
                  state,
                  CancellationToken.None);
            AddLog(LogDefinitions.CoilWrite, $"{reason}: {kItem.Code} -> {(state ? "ON" : "OFF")} (coil {kItem.Definition.CoilAddress})");
        }
        catch (Exception ex)
        {
            AddLog(LogDefinitions.CoilWriteFailed, $"{kItem.Code} write failed: {ex.Message}");
        }

        if (delayAfter)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private async Task<bool> ApplyKWithFeedbackCheckAsync(KContactViewModel kItem, string busName)
    {
        if (!State.Connection.IsConnected)
        {
            AddLog(LogDefinitions.BusApplyBlocked, $"{busName} apply blocked: Line Simulator disconnected.");
            return false;
        }

        for (var attempt = 1; attempt <= MaxApplyFeedbackRetryCount; attempt++)
        {
            await SetKStateAsync(kItem, true, $"{busName} apply attempt {attempt}", delayAfter: true);

            bool feedbackMatched;
            try
            {
                feedbackMatched = await ReadKFeedbackStateAsync(kItem, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AddLog(LogDefinitions.OnFeedbackVerificationFailed, $"{kItem.Code} feedback verification failed: {ex.Message}");
                await DisconnectCoreAsync("Disconnected automatically after feedback verification failure.", stopPolling: true);
                return false;
            }

            if (feedbackMatched)
            {
                AddLog(LogDefinitions.OnFeedbackConfirmed, $"{kItem.Code} feedback confirmed.");
                return true;
            }

            AddLog(LogDefinitions.OnFeedbackMismatch, $"{kItem.Code} feedback mismatch ({attempt}/{MaxApplyFeedbackRetryCount}).");
        }

        AddLog(LogDefinitions.OnFeedbackRetryExhausted, $"{kItem.Code} feedback retry exhausted. Alarm coil {AlarmCoilAddress} -> ON");
        AddLog(LogDefinitions.AlarmRetryExhausted, $"{kItem.Code} feedback retry exhausted. Alarm coil {AlarmCoilAddress} -> ON");
        //await WriteAlarmCoilAsync();
        return false;
    }

    private async Task<bool> TurnKOffWithFeedbackCheckAsync(KContactViewModel kItem, string reason)
    {
        if (!State.Connection.IsConnected)
        {
            AddLog(LogDefinitions.LineSimulatorDisconnected, $"{reason}: Line Simulator disconnected.");
            return false;
        }

        for (var attempt = 1; attempt <= MaxApplyFeedbackRetryCount; attempt++)
        {
            await SetKStateAsync(kItem, false, $"{reason} attempt {attempt}", delayAfter: true);

            try
            {
                var feedbackMatched = !await ReadKFeedbackStateAsync(kItem, CancellationToken.None);
                if (feedbackMatched)
                {
                    AddLog(LogDefinitions.OffFeedbackConfirmed, $"{kItem.Code} feedback confirmed OFF.");
                    return true;
                }

                AddLog(LogDefinitions.OffFeedbackMismatch, $"{kItem.Code} off feedback mismatch ({attempt}/{MaxApplyFeedbackRetryCount}).");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AddLog(LogDefinitions.OffFeedbackVerificationFailed, $"{kItem.Code} off feedback verification failed: {ex.Message}");
                await DisconnectCoreAsync("Disconnected automatically after off feedback verification failure.", stopPolling: true);
                return false;
            }
        }

        AddLog(LogDefinitions.OffFeedbackRetryExhausted, $"{kItem.Code} off feedback retry exhausted. Alarm coil {AlarmCoilAddress} -> ON");
        AddLog(LogDefinitions.AlarmRetryExhausted, $"{kItem.Code} off feedback retry exhausted. Alarm coil {AlarmCoilAddress} -> ON");
        //await WriteAlarmCoilAsync();
        return false;
    }

    private KContactViewModel? FindInterlockedPeer(KContactViewModel target)
    {
        if (!IsSharedMc(target.Definition.SourceMcNumber))
        {
            return null;
        }

        return KItems.FirstOrDefault(item =>
            !ReferenceEquals(item, target) &&
            item.Definition.SourceMcNumber == target.Definition.SourceMcNumber &&
            item.IsOn);
    }

    private static bool IsSharedMc(int sourceMcNumber)
    {
        return McCatalog.ByNumber.TryGetValue(sourceMcNumber, out var definition) && definition.IsShared;
    }

    private void AddLog(LogDefinition definition, string message) => LogStore.Append(definition, message);

    private bool CanApplyBus(string busName)
    {
        if (_isBusOperationRunning || !State.Connection.IsConnected || State.OperationMode.IsManualMode)
        {
            return false;
        }

        return busName switch
        {
            "BUS1" => !State.Bus1.IsApplied,
            "BUS2" => State.Bus2.IsEnabled && State.Bus1.IsApplied && !State.Bus2.IsApplied,
            "BUS3" => State.Bus3.IsEnabled && State.Bus2.IsApplied && !State.Bus3.IsApplied,
            _ => false,
        };
    }

    private bool CanOffBus(string busName)
    {
        if (_isBusOperationRunning || !State.Connection.IsConnected)
        {
            return false;
        }

        return HasActiveBusOutput(busName);
    }

    private void SetBusApplied(string busName, bool isApplied)
    {
        switch (busName)
        {
            case "BUS1":
                State.Bus1.IsApplied = isApplied;
                break;
            case "BUS2":
                State.Bus2.IsApplied = isApplied;
                break;
            case "BUS3":
                State.Bus3.IsApplied = isApplied;
                break;
        }
    }

    private void SetBusConfigurationLocked(string busName, bool isLocked)
    {
        switch (busName)
        {
            case "BUS1":
                State.Bus1.IsConfigurationLocked = isLocked;
                break;
            case "BUS2":
                State.Bus2.IsConfigurationLocked = isLocked;
                break;
            case "BUS3":
                State.Bus3.IsConfigurationLocked = isLocked;
                break;
        }
    }

    private void ResetBusAppliedFlags()
    {
        State.Bus1.IsApplied = false;
        State.Bus2.IsApplied = false;
        State.Bus3.IsApplied = false;
        State.Bus1.IsConfigurationLocked = false;
        State.Bus2.IsConfigurationLocked = false;
        State.Bus3.IsConfigurationLocked = false;
    }

    private bool IsBusApplied(string busName)
    {
        return busName switch
        {
            "BUS1" => State.Bus1.IsApplied,
            "BUS2" => State.Bus2.IsApplied,
            "BUS3" => State.Bus3.IsApplied,
            _ => false,
        };
    }

    private bool HasActiveBusOutput(string busName)
    {
        return KItems.Any(item =>
            string.Equals(item.TargetBus, busName, StringComparison.OrdinalIgnoreCase) &&
            item.IsOn);
    }

    private bool TryBeginBusOperation()
    {
        if (_isBusOperationRunning)
        {
            return false;
        }

        _isBusOperationRunning = true;
        RaiseBusCommandCanExecuteChanged();
        return true;
    }

    private void EndBusOperation()
    {
        _isBusOperationRunning = false;
        RaiseBusCommandCanExecuteChanged();
    }

    #endregion

    #region Shared Lifecycle
    public void RequestShutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _feedbackPollingCts?.Cancel();
        _ovrPollingCts?.Cancel();
        _idleMonitorCts?.Cancel();
    }
    #endregion

    #region EndPoint Idle Monitor
    private void StartIdleMonitor()
    {
        if (_idleMonitorTask is { IsCompleted: false })
        {
            return;
        }

        _idleMonitorCts = new CancellationTokenSource();
        _idleMonitorTask = MonitorIdleEndpointsAsync(_idleMonitorCts.Token);
    }

    private async Task StopIdleMonitorAsync()
    {
        if (_idleMonitorCts is null)
        {
            return;
        }

        _idleMonitorCts.Cancel();

        if (_idleMonitorTask is not null)
        {
            try
            {
                await _idleMonitorTask;
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
        }

        _idleMonitorCts.Dispose();
        _idleMonitorCts = null;
        _idleMonitorTask = null;
    }

    private async Task MonitorIdleEndpointsAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        await RefreshIdleStatusesAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshIdleStatusesAsync(cancellationToken);
        }
    }

    private async Task RefreshIdleStatusesAsync(CancellationToken cancellationToken)
    {
        var disabledEndpoints = State.OvrSettings.Endpoints
            .Where(endpoint => !endpoint.IsEnabled)
            .ToArray();

        if (disabledEndpoints.Length == 0)
        {
            return;
        }

        // OVR/PM이 활성화된 엔드포인트는 상태가 Idle이든 Disable이든 상관없이 OVR/PM에서 관리되므로, 여기서는 비활성화된 엔드포인트만 주기적으로 상태를 확인하여 Idle/Disable을 업데이트함
        var snapshots = await Task.WhenAll(disabledEndpoints.Select(endpoint => ProbeIdleStatusAsync(endpoint, cancellationToken))); 
        await ApplyIdleSnapshotsAsync(snapshots);
    }

    private async Task<EndpointIdleSnapshot> ProbeIdleStatusAsync(OvrEndpointSettingsModel endpoint, CancellationToken cancellationToken)
    {
        try
        {
            await endpoint.IoLock.WaitAsync(cancellationToken);
            try
            {
                if (endpoint.IsEnabled)
                {
                    return new EndpointIdleSnapshot(endpoint, null, null);
                }

                if (endpoint.Socket.IsConnected)
                {
                    await endpoint.Socket.DisconnectAsync();
                }
            }
            finally
            {
                endpoint.IoLock.Release();
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(endpoint.IpAddress))
            {
                return new EndpointIdleSnapshot(endpoint, EndpointStatus.Disable, "Ping skipped: empty IP");
            }

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(endpoint.IpAddress, 1000);
            var status = reply.Status == IPStatus.Success ? EndpointStatus.Idle : EndpointStatus.Disable;
            var info = reply.Status == IPStatus.Success
                ? $"Ping OK: {endpoint.IpAddress}"
                : $"Ping failed: {reply.Status}";
            return new EndpointIdleSnapshot(endpoint, status, info);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EndpointIdleSnapshot(endpoint, EndpointStatus.Disable, $"Ping failed: {ex.Message}");
        }
    }

    private async Task ApplyIdleSnapshotsAsync(IReadOnlyList<EndpointIdleSnapshot> snapshots)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyIdleSnapshots(snapshots);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ApplyIdleSnapshots(snapshots));
    }

    private void ApplyIdleSnapshots(IReadOnlyList<EndpointIdleSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Status is null || snapshot.Endpoint.IsEnabled)
            {
                continue;
            }

            snapshot.Endpoint.ApplyReadResult(
                isConnected: false,
                currentValue: null,
                statusText: snapshot.Status.Value);
            snapshot.Endpoint.ApplyPowerMeterMeasurements(null);
            snapshot.Endpoint.Info = snapshot.Info ?? string.Empty;
        }
    }

    // OVR/PM이 활성화된 엔드포인트가 하나라도 있으면 이 루프가 실행되어 OVR/PM 폴링을 수행함
    private async Task PollOvrLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        await RefreshOvrCurrentsAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshOvrCurrentsAsync(cancellationToken);
        }
    }
    private async Task RefreshOvrCurrentsAsync(CancellationToken cancellationToken)
    {
        // OVR/PM이 활성화된 엔드포인트는 ReadEndpointRegistersAsync에서 실제 OVR/PM 폴링을 수행하여 상태를 업데이트하고, 비활성화된 엔드포인트는 기존에 Idle/Disable 상태를 유지하면서 현재값은 null로 반환함
        var snapshots = await Task.WhenAll(State.OvrSettings.Endpoints.Select(endpoint => ReadEndpointRegistersAsync(endpoint, cancellationToken)));
        // OVR/PM이 활성화된 엔드포인트는 폴링 결과로 상태와 현재값이 업데이트되고, 비활성화된 엔드포인트는 기존 상태 유지하면서 현재값은 null로 업데이트되어 뷰에 반영됨
        await ApplyOvrSnapshotsAsync(snapshots); 
    }
    private async Task<EndpointReadSnapshot> ReadEndpointRegistersAsync(OvrEndpointSettingsModel endpoint, CancellationToken cancellationToken)
    {
        await endpoint.IoLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadEndpointSnapshotCoreAsync(endpoint, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }

            return new EndpointReadSnapshot(
                endpoint,
                false,
                null,
                EndpointStatus.Disable,
                [],
                Source: EndpointSnapshotSource.Polling,
                Message: ex.Message);
        }
        finally
        {
            endpoint.IoLock.Release();
        }
    }
    // OVR/PM이 활성화된 엔드포인트가 하나라도 있으면 OVR/PM 폴링을 시작하고, 그렇지 않으면 폴링을 중지하는 메서드.
    private async Task RestartOvrPollingAsync()
    {
        await StopOvrPollingAsync(disconnectSockets: false);

        if (!State.OvrSettings.Endpoints.Any(endpoint => endpoint.IsEnabled))
        {
            await RefreshIdleStatusesAsync(CancellationToken.None);
            AddLog(LogDefinitions.EndpointPollingStopped, "OVR polling stopped.");
            return;
        }

        _ovrPollingCts = new CancellationTokenSource();
        _ovrPollingTask = PollOvrLoopAsync(_ovrPollingCts.Token);
        AddLog(LogDefinitions.EndpointPollingStarted, "OVR polling started.");
    }

    private async Task StopOvrPollingAsync(bool disconnectSockets = true)
    {
        if (_ovrPollingCts is not null)
        {
            _ovrPollingCts.Cancel();

            if (_ovrPollingTask is not null)
            {
                try
                {
                    await _ovrPollingTask;
                }
                catch (OperationCanceledException)
                {
                    // normal shutdown
                }
            }

            _ovrPollingCts.Dispose();
            _ovrPollingCts = null;
            _ovrPollingTask = null;
        }

        if (!disconnectSockets)
        {
            return;
        }

        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }
        }
    }

    #endregion

    #region EndPoint Communication
    private async Task ApplyOvrSettingsAsync()
    {
        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            endpoint.IsEnabled = endpoint.PendingIsEnabled;
        }

        await StopOvrPollingAsync(disconnectSockets: false);

        var snapshots = await Task.WhenAll(State.OvrSettings.Endpoints.Select(endpoint => ConfigureEndpointAsync(endpoint, CancellationToken.None)));
        await ApplyOvrSnapshotsAsync(snapshots);
        await RefreshIdleStatusesAsync(CancellationToken.None);

        var enabledNames = State.OvrSettings.Endpoints
            .Where(endpoint => endpoint.IsEnabled)
            .Select(endpoint => endpoint.Name)
            .ToArray();

        if (enabledNames.Length == 0)
        {
            SyncOvrCurrentsToDiagram(new Dictionary<string, double?>());
            AddLog(LogDefinitions.EndpointSettingsEmpty, "OVR/PM endpoint apply: nothing selected.");
            return;
        }

        AddLog(LogDefinitions.EndpointSettingsApplied, $"OVR/PM endpoint apply: {string.Join(", ", enabledNames)}");
        await RestartOvrPollingAsync();
    }

    private void ResetOvrEndpointStates()
    {
        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            endpoint.ApplyReadResult(
                isConnected: false,
                currentValue: null,
                statusText: endpoint.IsEnabled ? EndpointStatus.Idle : EndpointStatus.Disable);
        }
    }
    private async Task ApplyOvrSnapshotsAsync(IReadOnlyList<EndpointReadSnapshot> snapshots)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            // 폴링 루프에서 이미 백그라운드 스레드이므로, 현재 스레드가 UI 스레드인지 확인하여 UI 스레드가 아니면 디스패처를 통해 UI 업데이트를 수행함
            ApplyOvrSnapshots(snapshots);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ApplyOvrSnapshots(snapshots));
    }
    private void ApplyOvrSnapshots(IReadOnlyList<EndpointReadSnapshot> snapshots)
    {
        var currentValues = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.Endpoint.IsEnabled)
            {
                continue;
            }

            var wasConnected = snapshot.Endpoint.IsConnected;

            if (snapshot.Source == EndpointSnapshotSource.Polling)
            {
                if (wasConnected && !snapshot.IsConnected)
                {
                    AddLog(
                        LogDefinitions.EndpointPollingReadFailed,
                        $"{snapshot.Endpoint.Name} polling read failed: {snapshot.Message ?? "Disconnected"}");
                }
                else if (!wasConnected && snapshot.IsConnected)
                {
                    AddLog(
                        LogDefinitions.EndpointConnectionRecovered,
                        $"{snapshot.Endpoint.Name} connection recovered.");
                }
            }

            // 각 스냅샷의 연결 상태, 현재값, 상태 텍스트, 레지스터 값을 엔드포인트에 적용하여 뷰에 반영함
            snapshot.Endpoint.ApplyReadResult(snapshot.IsConnected, snapshot.CurrentValue, snapshot.Status, snapshot.Registers);
            snapshot.Endpoint.ApplyPowerMeterMeasurements(snapshot.PowerMeterMeasurement);
            currentValues[snapshot.Endpoint.DeviceKey] = snapshot.CurrentValue;
        }

        SyncOvrCurrentsToDiagram(currentValues);
    }

    #endregion

    #region EndPoint Register Access
    public async Task<ushort[]> ReadOvrEndpointHoldingRegistersAsync(
        string endpointName,
        ushort startAddress,
        ushort numberOfPoints,
        CancellationToken cancellationToken = default)
    {
        var endpoint = FindEndpointOrThrow(endpointName);
        await endpoint.IoLock.WaitAsync(cancellationToken);
        try
        {
            if (!endpoint.IsEnabled)
            {
                throw new InvalidOperationException($"OVR/PM endpoint '{endpoint.Name}' is not enabled.");
            }

            await endpoint.Socket.EnsureConnectedAsync(endpoint.IpAddress, endpoint.Port, cancellationToken);

            var slaveId = ResolveSlaveId(endpoint);
            return await endpoint.Socket.ReadHoldingRegistersAsync(slaveId, startAddress, numberOfPoints, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }

            throw;
        }
        finally
        {
            endpoint.IoLock.Release();
        }
    }

    public async Task WriteOvrEndpointRegisterAsync(string endpointName, ushort registerAddress, ushort value, CancellationToken cancellationToken = default) // Write Single Register
    {
        var endpoint = FindEndpointOrThrow(endpointName);
        var snapshot = await WriteEndpointRegistersAsync(endpoint, registerAddress, [value], writeSingleRegister: true, cancellationToken);
        await ApplyOvrSnapshotsAsync([snapshot]);
        AddLog(
            snapshot.IsConnected ? LogDefinitions.EndpointRegisterWrite : LogDefinitions.EndpointRegisterWriteFailed,
            snapshot.IsConnected
                ? $"{endpoint.Name} register write: {registerAddress} = {value}"
                : $"{endpoint.Name} register write failed");
    }

    public async Task WriteOvrEndpointRegistersAsync(string endpointName, ushort startAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default) // Write Multiple Registers
    {
        var endpoint = FindEndpointOrThrow(endpointName);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one register value is required.", nameof(values));
        }

        var snapshot = await WriteEndpointRegistersAsync(endpoint, startAddress, values, writeSingleRegister: false, cancellationToken);
        await ApplyOvrSnapshotsAsync([snapshot]);
        AddLog(
            snapshot.IsConnected ? LogDefinitions.EndpointRegisterBlockWrite : LogDefinitions.EndpointRegisterBlockWriteFailed,
            snapshot.IsConnected
                ? $"{endpoint.Name} register block write: {startAddress}~{startAddress + values.Count - 1} ({values.Count} words)"
                : $"{endpoint.Name} register block write failed");
    }
    private async Task<EndpointReadSnapshot> WriteEndpointRegistersAsync(
        OvrEndpointSettingsModel endpoint,
        ushort startAddress,
        IReadOnlyList<ushort> values,
        bool writeSingleRegister,
        CancellationToken cancellationToken)
    {
        await endpoint.IoLock.WaitAsync(cancellationToken);
        try
        {
            if (!endpoint.IsEnabled)
            {
                return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, [], Source: EndpointSnapshotSource.Write);
            }

            await endpoint.Socket.EnsureConnectedAsync(endpoint.IpAddress, endpoint.Port, cancellationToken);

            var slaveId = ResolveSlaveId(endpoint); // Slave ID는 Endpoint 설정의 SlaveId 또는 UnitId에서 가져오며, 1~247 범위로 클램프됨

            if (writeSingleRegister)
            {
                await endpoint.Socket.WriteSingleRegisterAsync(slaveId, startAddress, values[0], cancellationToken);
            }
            else
            {
                await endpoint.Socket.WriteMultipleRegistersAsync(slaveId, startAddress, values.ToArray(), cancellationToken);
            }

            return await ReadEndpointSnapshotCoreAsync(endpoint, cancellationToken, $"Write OK / {startAddress}~{startAddress + values.Count - 1}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }

            return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, [], Source: EndpointSnapshotSource.Write);
        }
        finally
        {
            endpoint.IoLock.Release();
        }
    }
    private static byte ResolveSlaveId(OvrEndpointSettingsModel endpoint)
    {
        var requestId = endpoint.SlaveId > 0 ? endpoint.SlaveId : endpoint.UnitId;
        return (byte)Math.Clamp(requestId, 1, 247); // Modbus 프로토콜에서 유효한 Slave ID 범위는 1~247
    }

    private OvrEndpointSettingsModel FindEndpointOrThrow(string endpointName)
    {
        return State.OvrSettings.Endpoints.FirstOrDefault(
                   endpoint => string.Equals(endpoint.Name, endpointName, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(endpoint.DeviceKey, endpointName, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"OVR/PM endpoint '{endpointName}' was not found.");
    }

    private async Task<EndpointReadSnapshot> ConfigureEndpointAsync(OvrEndpointSettingsModel endpoint, CancellationToken cancellationToken)
    {
        await endpoint.IoLock.WaitAsync(cancellationToken);
        try
        {
            if (!endpoint.IsEnabled)
            {
                await endpoint.Socket.DisconnectAsync();
                return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, [], Source: EndpointSnapshotSource.Configuration);
            }

            await endpoint.Socket.EnsureConnectedAsync(endpoint.IpAddress, endpoint.Port, cancellationToken);

            return new EndpointReadSnapshot(endpoint, true, endpoint.CurrentValue, EndpointStatus.Enable, endpoint.RegisterSnapshot.ToArray(), Source: EndpointSnapshotSource.Configuration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }

            return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, [], Source: EndpointSnapshotSource.Configuration);
        }
        finally
        {
            endpoint.IoLock.Release();
        }
    }

    private async Task<EndpointReadSnapshot> ReadEndpointSnapshotCoreAsync(
        OvrEndpointSettingsModel endpoint,
        CancellationToken cancellationToken,
        string? statusPrefix = null)
    {
        if (!endpoint.IsEnabled)
        {
            return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, [], Source: EndpointSnapshotSource.Polling);
        }

        await endpoint.Socket.EnsureConnectedAsync(endpoint.IpAddress, endpoint.Port, cancellationToken);

        var slaveId = ResolveSlaveId(endpoint);
        var protocolProfile = endpoint.ProtocolProfile;
        var readStartAddress = protocolProfile.ReadStartAddress;
        var readRegisterCount = protocolProfile.ReadRegisterCount;

        var registers = endpoint.ReadFromInputRegisters
            ? await endpoint.Socket.ReadInputRegistersAsync(slaveId, readStartAddress, readRegisterCount, cancellationToken)
            : await endpoint.Socket.ReadHoldingRegistersAsync(slaveId, readStartAddress, readRegisterCount, cancellationToken);

        PowerMeterMeasurementSnapshot? powerMeterMeasurement = endpoint.IsPowerMeter
            ? ParsePowerMeterMeasurements(endpoint, registers, readStartAddress)
            : null;

        double? currentValue = null;
        if(endpoint.IsPowerMeter) currentValue = powerMeterMeasurement?.AverageCurrent;
        else
        {
            var currentRegisterAddress = ResolveCurrentRegisterAddress(endpoint);
            if (currentRegisterAddress >= readStartAddress &&
                currentRegisterAddress + 1 < readStartAddress + registers.Length)
            {
                var registerIndex = currentRegisterAddress - readStartAddress;
                uint rawValue = ((uint)registers[registerIndex] << 16) | registers[registerIndex + 1];

                var registerDefinition = endpoint.RegisterDefinitions.FirstOrDefault(
                    definition => definition.Address == endpoint.CurrentRegisterAddress);

                currentValue = registerDefinition?.Format == EndpointValueFormat.Ieee754Float32
                    ? BitConverter.Int32BitsToSingle((int)rawValue)
                    : rawValue * endpoint.CurrentScale;
            }
        }

        EndpointStatus status = EndpointStatus.Enable;

        return new EndpointReadSnapshot(endpoint, true, currentValue, status, registers, powerMeterMeasurement, EndpointSnapshotSource.Polling);
    }

    private static PowerMeterMeasurementSnapshot ParsePowerMeterMeasurements(
        OvrEndpointSettingsModel endpoint,
        IReadOnlyList<ushort> registers,
        ushort readStartAddress)
    {
        return new PowerMeterMeasurementSnapshot(
            AverageVoltage: TryReadFloatRegister(endpoint, registers, readStartAddress, 30001),
            AverageCurrent: TryReadFloatRegister(endpoint, registers, readStartAddress, 30003),
            PowerFactor: TryReadFloatRegister(endpoint, registers, readStartAddress, 30023),
            TotalActivePower: TryReadFloatRegister(endpoint, registers, readStartAddress, 30025),
            Frequency: TryReadFloatRegister(endpoint, registers, readStartAddress, 30031));
    }

    private static double? TryReadFloatRegister(
        OvrEndpointSettingsModel endpoint,
        IReadOnlyList<ushort> registers,
        ushort readStartAddress,
        int address)
    {
        var registerDefinition = endpoint.RegisterDefinitions.FirstOrDefault(
            definition => definition.Address == address);

        if (registerDefinition is null)
        {
            return null;
        }

        var registerIndex = registerDefinition.ModbusStartAddress - readStartAddress;
        if (registerIndex < 0 || registerIndex + 1 >= registers.Count)
        {
            return null;
        }

        uint rawValue = ((uint)registers[registerIndex] << 16) | registers[registerIndex + 1];
        return BitConverter.Int32BitsToSingle((int)rawValue);
    }

    private static ushort ResolveCurrentRegisterAddress(OvrEndpointSettingsModel endpoint)
    {
        var registerDefinition = endpoint.RegisterDefinitions.FirstOrDefault(
            definition => definition.Address == endpoint.CurrentRegisterAddress);

        if (registerDefinition is not null)
        {
            return registerDefinition.ModbusStartAddress;
        }

        return (ushort)Math.Max(0, endpoint.CurrentRegisterAddress);
    }

    private enum EndpointSnapshotSource
    {
        Polling,
        Configuration,
        Write,
    }

    private sealed record EndpointReadSnapshot(
        OvrEndpointSettingsModel Endpoint,
        bool IsConnected,
        double? CurrentValue,
        EndpointStatus Status,
        IReadOnlyList<ushort> Registers,
        PowerMeterMeasurementSnapshot? PowerMeterMeasurement = null,
        EndpointSnapshotSource Source = EndpointSnapshotSource.Polling,
        string? Message = null);

    private sealed record EndpointIdleSnapshot(
        OvrEndpointSettingsModel Endpoint,
        EndpointStatus? Status,
        string? Info);
    #endregion

    #region MainWindow Command State
    private void RaiseModeAndConfigurationStateChanged()
    {
        RaisePropertyChanged(nameof(CanEditBus1Settings));
        RaisePropertyChanged(nameof(CanEditBus2Settings));
        RaisePropertyChanged(nameof(CanEditBus3Settings));
        RaisePropertyChanged(nameof(CanEditBus2Usage));
        RaisePropertyChanged(nameof(CanEditBus3Usage));
        RaiseBusCommandCanExecuteChanged();
    }

    private bool AreAllBusOutputsOpen()
    {
        return !State.Bus1.IsApplied &&
               !State.Bus2.IsApplied &&
               !State.Bus3.IsApplied &&
               !KItems.Any(item => IsBusTarget(item.TargetBus) && item.IsOn);
    }

    private static bool IsBusTarget(string targetBus)
    {
        return targetBus is "BUS1" or "BUS2" or "BUS3";
    }

    private void RaiseBusCommandCanExecuteChanged()
    {
        Bus1ApplyCommand.RaiseCanExecuteChanged();
        Bus2ApplyCommand.RaiseCanExecuteChanged();
        Bus3ApplyCommand.RaiseCanExecuteChanged();
        Bus1OffCommand.RaiseCanExecuteChanged();
        Bus2OffCommand.RaiseCanExecuteChanged();
        Bus3OffCommand.RaiseCanExecuteChanged();
    }
    #endregion

    #region LineSimulator Feedback / IO
    private void StartFeedbackPolling()
    {
        if (_feedbackPollingTask is { IsCompleted: false })
        {
            return;
        }

        _feedbackPollingCts = new CancellationTokenSource();
        _feedbackPollingTask = PollFeedbackLoopAsync(_feedbackPollingCts.Token);
    }

    private async Task StopFeedbackPollingAsync()
    {
        if (_feedbackPollingCts is null)
        {
            return;
        }

        _feedbackPollingCts.Cancel();

        if (_feedbackPollingTask is not null)
        {
            try
            {
                await _feedbackPollingTask;
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
        }

        _feedbackPollingCts.Dispose();
        _feedbackPollingCts = null;
        _feedbackPollingTask = null;
    }

    private async Task PollFeedbackLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        await RefreshFeedbackAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshFeedbackAsync(cancellationToken);
        }
    }

    private async Task RefreshFeedbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            var values = await _modbusGatewayService.ReadDiscreteInputsAsync(
                (byte)State.Connection.UnitId,
                FeedbackStartAddress,
                FeedbackCount,
                cancellationToken);

            var reg = await _modbusGatewayService.ReadInputRegistersAsync(
                (byte)State.Connection.UnitId,
                LineRegisterStartAddress,
                LineRegisterCount,
                cancellationToken);

            await UpdateFeedbackStatesAsync(values, reg);

            if (_feedbackReadFailed)
            {
                AddLog(LogDefinitions.FeedbackRecovered, "Discrete input feedback recovered.");
                _feedbackReadFailed = false;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!_feedbackReadFailed)
            {
                AddLog(LogDefinitions.FeedbackReadFailed, $"Feedback read failed: {ex.Message}");
            }

            _feedbackReadFailed = true;
            await DisconnectCoreAsync("Disconnected automatically after feedback read failure.", stopPolling: false);
        }
    }
    private async Task UpdateFeedbackStatesAsync(IReadOnlyList<bool> values, IReadOnlyList<ushort> reg)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ApplyFeedbackStates(values);
            ApplyRegisterStates(reg);

            return;
        }

        if (dispatcher.CheckAccess())
        {
            ApplyFeedbackStates(values);
            ApplyRegisterStates(reg);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ApplyFeedbackStates(values));
        await dispatcher.InvokeAsync(() => ApplyRegisterStates(reg));
    }
    private void ApplyRegisterStates(IReadOnlyList<ushort> values)
    {
        if (values == null || values.Count == 0) return;

        App app = Application.Current as App;
        var bytes = app.ConvertFunction.RegistersToBytes(values, false);
        
        NBusOut1 = BitConverter.ToSingle(bytes, 0);
        NBusOut2 = BitConverter.ToSingle(bytes, 4);
        NBusOut3 = BitConverter.ToSingle(bytes, 8);
    }
    private void ApplyFeedbackStates(IReadOnlyList<bool> values)
    {
        foreach (var kItem in KItems)
        {
            var index = kItem.Definition.FeedbackAddress - FeedbackStartAddress;
            kItem.IsOn = index >= 0 && index < values.Count && values[index];
        }

        SyncBusDiagramFeedback();
        RaiseBusCommandCanExecuteChanged();
    }

    private async Task DisconnectCoreAsync(string logMessage, bool stopPolling)
    {
        if (stopPolling)
        {
            await StopFeedbackPollingAsync();
        }
        else
        {
            _feedbackPollingCts?.Cancel();
        }

        await _modbusGatewayService.DisconnectAsync();
        State.Connection.IsConnected = false;
        ResetBusAppliedFlags();
        await UpdateFeedbackStatesAsync([],[]);
        _feedbackReadFailed = false;
        AddLog(LogDefinitions.LineSimulatorDisconnected, logMessage);
    }

    #region LineSimulator Modbus Access
    // Write coil
    private async Task WriteAlarmCoilAsync() // 알람 코일은 시스템이 비정상 상태에 빠졌을 때 PLC에서 감지하여 자체적으로 복구 시도를 하거나 관리자에게 경고하기 위한 용도
    {
        if (!State.Connection.IsConnected)
        {
            AddLog(LogDefinitions.AlarmCoilWriteSkipped, $"Alarm coil {AlarmCoilAddress} write skipped: disconnected");
            return;
        }

        try
        {
            //await WriteSingleCoilAsync((byte)State.Connection.UnitId, AlarmCoilAddress, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            AddLog(LogDefinitions.AlarmCoilWriteFailed, $"Alarm coil write failed: {ex.Message}");
            await DisconnectCoreAsync("Disconnected automatically after alarm coil write failure.", stopPolling: true);
        }
    }
    private async Task WriteSingleCoilAsync(byte unitId, ushort coilAddress, bool value, CancellationToken cancellationToken)
    {
        await _modbusIoLock.WaitAsync(cancellationToken);
        try
        {
            await _modbusGatewayService.WriteSingleCoilAsync(unitId, coilAddress, value, cancellationToken);
        }
        finally
        {
            _modbusIoLock.Release();
        }
    }
    // Read discrete input
    private async Task<bool> ReadKFeedbackStateAsync(KContactViewModel kItem, CancellationToken cancellationToken)
    {
        var values = await ReadDiscreteInputsAsync(
            (byte)State.Connection.UnitId,
            kItem.Definition.FeedbackAddress,
            1,
            cancellationToken);

        var isOn = values.Length > 0 && values[0];
        await UpdateSingleFeedbackStateAsync(kItem, isOn);
        return isOn;
    }
    private async Task<bool[]> ReadDiscreteInputsAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken)
    {
        await _modbusIoLock.WaitAsync(cancellationToken);
        try
        {
            return await _modbusGatewayService.ReadDiscreteInputsAsync(unitId, startAddress, numberOfPoints, cancellationToken);
        }
        finally
        {
            _modbusIoLock.Release();
        }
    }
    // Read registers
    private async Task<ushort[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken)
    {
        await _modbusIoLock.WaitAsync(cancellationToken);
        try
        {
            return await _modbusGatewayService.ReadHoldingRegistersAsync(unitId, startAddress, numberOfPoints, cancellationToken);
        }
        finally
        {
            _modbusIoLock.Release();
        }
    }
    
    #endregion

    private async Task UpdateSingleFeedbackStateAsync(KContactViewModel kItem, bool isOn)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            kItem.IsOn = isOn;
            SyncBusDiagramFeedback();
            RaiseBusCommandCanExecuteChanged();
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            kItem.IsOn = isOn;
            SyncBusDiagramFeedback();
            RaiseBusCommandCanExecuteChanged();
        });
    }
    #endregion

    #region MainWindow Diagram Synchronization
    private void SyncBusDiagramFeedback()
    {
        var feedbackStates = KItems.ToDictionary(item => item.Code, item => item.IsOn);
        BusDiagram.SynchronizeFeedback(feedbackStates);
    }

    private void SyncOvrCurrentsToDiagram(IReadOnlyDictionary<string, double?> currentValues)
    {
        BusDiagram.UpdateMarkerCurrents(currentValues);
    }
    #endregion

    #region Shared Dispose
    public void Dispose()
    {
        RequestShutdown();
        UnsubscribeStateEvents();

        try
        {
            WaitForShutdownTask(State.OvrSettings.DisposeAsync().AsTask());
        }
        catch
        {
            // shutdown path
        }

        try
        {
            WaitForShutdownTask(_modbusGatewayService.DisposeAsync().AsTask());
        }
        catch
        {
            // shutdown path
        }

        try
        {
            _modbusIoLock.Dispose();
        }
        catch
        {
            // shutdown path
        }
    }

    private static void WaitForShutdownTask(Task task)
    {
        try
        {
            if (task.Wait(TimeSpan.FromMilliseconds(500)))
            {
                task.GetAwaiter().GetResult();
            }
        }
        catch
        {
            // shutdown path
        }
    }
    #endregion
}
